using System.ComponentModel;
using System.Windows;
using Vitral.Core.Barra;
using Vitral.Core.Perfis;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// A barra do topo de verdade: a reserva, as três ilhas e as fontes de dados. Nunca lança (contrato de
/// <see cref="IControleBarra"/>): erro vai para o log e, se a reserva falhar, as ilhas aparecem mesmo assim com um aviso.
internal sealed class ControleBarra(Action<string> avisar) : IControleBarra
{
    private ReservaTopo? _reserva;
    private Fontes? _fontes;
    private JanelaIlha? _midia, _relogio, _status;
    private IlhaMidiaViewModel? _vmMidia;
    private IlhaRelogioViewModel? _vmRelogio;
    private IlhaStatusViewModel? _vmStatus;
    private BarraTopoPerfil _estado = BarraTopoPerfil.Desligada;

    // Painéis próprios (spec v2): um por vez, decidido pelo coordenador; as janelas nascem na primeira abertura.
    private readonly CoordenadorPaineis _coordenador = new();
    private readonly Dictionary<Painel, JanelaPainel> _paineis = [];
    private CalendarioViewModel? _vmCalendario;
    private PainelBateriaViewModel? _vmBateria;
    private PainelWifiViewModel? _vmWifi;
    private readonly List<IDisposable> _vmsPaineis = [];
    private bool _coordenadorLigado;

    /// As fontes vivem enquanto a barra está ligada (COM e WinRT são soltos ao desligar).
    private sealed class Fontes : IDisposable
    {
        public FonteMidiaWindows Midia { get; } = new(Application.Current.Dispatcher);
        public FonteVolumeWindows Volume { get; } = new(Application.Current.Dispatcher);
        public FonteRedeWindows Rede { get; } = new(Application.Current.Dispatcher);
        public FonteBateriaWindows Bateria { get; } = new(Application.Current.Dispatcher);
        public FonteDispositivosAudioWindows Dispositivos { get; } = new(Application.Current.Dispatcher);
        public FonteModoEnergiaWindows ModoEnergia { get; } = new();
        public FonteWifiWindows Wifi { get; } = new(Application.Current.Dispatcher);
        public FonteDesempenhoWindows Desempenho { get; } = new();
        public RelogioDoSistema Agora { get; } = new();

        public void Dispose()
        {
            Midia.Dispose();
            Volume.Dispose();
            Rede.Dispose();
            Bateria.Dispose();
            Dispositivos.Dispose();
            Wifi.Dispose();
        }
    }

    public void Aplicar(BarraTopoPerfil estado)
    {
        try
        {
            _estado = estado;
            if (estado.Visivel) Abrir(estado.IlhasEfetivas);
            else Fechar();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("ControleBarra.Aplicar", e);
            avisar("A barra do topo não pôde ser ajustada. Detalhes no log.");
        }
    }

    private void Abrir(IlhasBarra ilhas)
    {
        if (_reserva is null)
        {
            _reserva = new ReservaTopo();
            _reserva.Recriar += AoRecriar;
            _reserva.TelaCheia += AoMudarTelaCheia;
        }
        if (!_reserva.Registrar()) avisar("Não foi possível reservar o espaço da barra do topo.");
        _fontes ??= new Fontes();
        if (!_coordenadorLigado)
        {
            _coordenador.PropertyChanged += AoMudarPainel;
            _coordenadorLigado = true;
        }
        _coordenador.FecharTodos(); // as ilhas podem ter mudado

        // Conteúdo refeito a cada aplicação: as ilhas (e CPU/MEM) podem ter mudado no perfil.
        DescartarViewModels();
        if (ilhas.Midia)
        {
            _vmMidia = new IlhaMidiaViewModel(_fontes.Midia);
            _vmMidia.PropertyChanged += AoMudarMidia;
        }
        if (ilhas.Relogio) _vmRelogio = new IlhaRelogioViewModel(_fontes.Agora, new RelogioWpf());
        if (ilhas.Status)
            _vmStatus = new IlhaStatusViewModel(_fontes.Desempenho, _fontes.Volume, _fontes.Rede, _fontes.Bateria, new RelogioWpf(), ilhas);

        _midia = Ajustar(_midia, _vmMidia, "Mídia", () => new IlhaMidia());
        _relogio = Ajustar(_relogio, _vmRelogio, "Relógio", () => new IlhaRelogio());
        _status = Ajustar(_status, _vmStatus, "Status", () => new IlhaStatus());
        AtualizarVisibilidadeMidia();
        Posicionar();
    }

    private JanelaIlha? Ajustar(JanelaIlha? janela, object? vm, string nome, Func<FrameworkElement> criar)
    {
        if (vm is null)
        {
            janela?.Close();
            return null;
        }
        if (janela is null)
        {
            var conteudo = criar();
            if (conteudo is IPedePainel pede) pede.Pediu += p => _coordenador.Alternar(p, DateTime.Now);
            janela = new JanelaIlha(nome, presaNaBorda: nome == "Relógio") { Conteudo = conteudo };
            janela.SizeChanged += (_, _) => Posicionar();
            janela.DpiChanged += (_, _) => Reajustar(); // troca de escala nem sempre manda WM_DISPLAYCHANGE
        }
        janela.DataContext = vm;
        return janela;
    }

    private void AoMudarMidia(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IlhaMidiaViewModel.Visivel)) return;
        // Nada mais tocando: a ilha some e o painel da mídia, se aberto, não pode ficar flutuando sem ela.
        if (_vmMidia?.Visivel != true && _coordenador.Aberto == Painel.Midia) _coordenador.FecharTodos();
        AtualizarVisibilidadeMidia();
    }

    private bool _telaCheia;

    /// A ilha de mídia some quando nada toca; todas somem com um app em tela cheia.
    private void AtualizarVisibilidadeMidia()
    {
        Mostrar(_midia, !_telaCheia && _vmMidia?.Visivel == true);
        Mostrar(_relogio, !_telaCheia);
        Mostrar(_status, !_telaCheia);
    }

    private static void Mostrar(JanelaIlha? janela, bool visivel)
    {
        if (janela is null) return;
        if (visivel && !janela.IsVisible) janela.Show();
        else if (!visivel && janela.IsVisible) janela.Hide();
    }

    private void AoMudarPainel(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CoordenadorPaineis.Aberto)) return;
        try
        {
            var aberto = _coordenador.Aberto;
            foreach (var (painel, janela) in _paineis)
                if (painel != aberto && janela.IsVisible) janela.Hide();
            if (aberto == Painel.Nenhum) return;

            var janelaPainel = JanelaDoPainel(aberto);
            var ilha = aberto switch { Painel.Calendario => _relogio, Painel.Midia => _midia, _ => _status };
            if (janelaPainel is null || ilha is null)
            {
                _coordenador.FecharTodos(); // painel ainda não existe nesta versão
                return;
            }
            var alinhamento = aberto switch
            {
                Painel.Calendario => AlinhamentoPainel.Centro,
                Painel.Midia => AlinhamentoPainel.Esquerda,
                _ => AlinhamentoPainel.Direita,
            };
            var p = MonitorPrincipal.Ler();
            var monitor = PosicaoIlhas.ParaDips(p.X, p.Y, p.Largura, p.Altura, p.Dpi);
            var ret = new Retangulo(ilha.Left, ilha.Top, ilha.ActualWidth, ilha.ActualHeight);
            janelaPainel.Mostrar(PosicaoIlhas.Painel(ret, janelaPainel.Width, janelaPainel.ActualHeight, alinhamento, monitor));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogBarra.Erro("ControleBarra.Painel", ex);
        }
    }

    /// A janela do painel, criada na primeira abertura; null se o painel ainda não existe.
    private JanelaPainel? JanelaDoPainel(Painel painel)
    {
        if (_paineis.TryGetValue(painel, out var existente))
        {
            if (painel == Painel.Calendario) _vmCalendario?.Reabrir();
            if (painel == Painel.Bateria) _vmBateria?.Abrir();
            if (painel == Painel.Wifi) _ = _vmWifi?.AbrirAsync();
            return existente;
        }
        JanelaPainel? nova = null;
        switch (painel)
        {
            case Painel.Calendario when _fontes is not null:
                _vmCalendario = new CalendarioViewModel(_fontes.Agora);
                nova = new JanelaPainel("Calendário", 248) { Conteudo = new PainelCalendario(), DataContext = _vmCalendario };
                break;
            case Painel.Volume when _fontes is not null:
                nova = new JanelaPainel("Volume", 280) { Conteudo = new PainelVolume(), DataContext = Guardar(new PainelVolumeViewModel(_fontes.Volume, _fontes.Dispositivos)) };
                break;
            case Painel.Bateria when _fontes is not null:
                _vmBateria = Guardar(new PainelBateriaViewModel(_fontes.Bateria, _fontes.ModoEnergia));
                nova = new JanelaPainel("Bateria", 320) { Conteudo = new PainelBateria(), DataContext = _vmBateria };
                break;
            case Painel.Wifi when _fontes is not null:
                _vmWifi = Guardar(new PainelWifiViewModel(_fontes.Wifi));
                nova = new JanelaPainel("Wi-Fi", 280) { Conteudo = new PainelWifi(), DataContext = _vmWifi };
                _ = _vmWifi.AbrirAsync();
                break;
            case Painel.Midia when _fontes is not null:
                nova = new JanelaPainel("Mídia", 260)
                {
                    Conteudo = new PainelMidia(),
                    DataContext = Guardar(new PainelMidiaViewModel(_fontes.Midia, new RelogioWpf(), _fontes.Agora)),
                };
                break;
        }
        if (nova is null) return null;
        nova.PerdeuFoco += () => _coordenador.FecharPorFoco(painel, DateTime.Now);
        nova.PediuFechar += _coordenador.FecharTodos;
        _paineis[painel] = nova;
        return nova;
    }

    private T Guardar<T>(T vm) where T : IDisposable
    {
        _vmsPaineis.Add(vm);
        return vm;
    }

    private void FecharPaineis()
    {
        _coordenador.FecharTodos();
        foreach (var janela in _paineis.Values) janela.Close();
        _paineis.Clear();
        _vmCalendario = null;
        _vmBateria = null;
        _vmWifi = null;
        foreach (var vm in _vmsPaineis) vm.Dispose();
        _vmsPaineis.Clear();
    }

    private void AoMudarTelaCheia(bool telaCheia)
    {
        LogBarra.Info($"Tela cheia: {telaCheia}");
        if (telaCheia) _coordenador.FecharTodos();
        _telaCheia = telaCheia;
        AtualizarVisibilidadeMidia();
    }

    /// Recoloca as ilhas no monitor principal atual (DIPs).
    public void Posicionar()
    {
        try
        {
            var p = MonitorPrincipal.Ler(); // as ilhas ficam no principal: DIPs pela escala atual dele
            var monitor = PosicaoIlhas.ParaDips(p.X, p.Y, p.Largura, p.Altura, p.Dpi);
            var (m, r, s) = PosicaoIlhas.Calcular(monitor, _midia?.ActualWidth ?? 0, _relogio?.ActualWidth ?? 0, _status?.ActualWidth ?? 0);
            Colocar(_midia, m);
            Colocar(_relogio, r);
            Colocar(_status, s);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleBarra.Posicionar", e); }
    }

    private static void Colocar(JanelaIlha? janela, Retangulo r)
    {
        if (janela is null) return;
        janela.Left = r.X;
        janela.Top = r.Y;
    }

    private void AoRecriar()
    {
        if (!_estado.Visivel || _reserva is null) return;
        if (!_reserva.Registrar()) avisar("Não foi possível reservar o espaço da barra do topo.");
        Posicionar();
    }

    /// Monitores, escala ou volta da suspensão: reserva, posição e dados de novo.
    public void Reajustar()
    {
        if (!_estado.Visivel) return;
        try
        {
            _reserva?.Reposicionar();
            Posicionar();
            _vmStatus?.Reler();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleBarra.Reajustar", e); }
    }

    private void DescartarViewModels()
    {
        if (_vmMidia is not null) _vmMidia.PropertyChanged -= AoMudarMidia;
        _vmMidia?.Dispose();
        _vmRelogio?.Dispose();
        _vmStatus?.Dispose();
        _vmMidia = null;
        _vmRelogio = null;
        _vmStatus = null;
    }

    /// Fecha as ilhas, solta as fontes e libera a reserva (perfil desligou a barra ou o Vitral está saindo).
    public void Fechar()
    {
        try
        {
            FecharPaineis();
            _midia?.Close();
            _relogio?.Close();
            _status?.Close();
            _midia = _relogio = _status = null;
            DescartarViewModels();
            _fontes?.Dispose();
            _fontes = null;
            _telaCheia = false;
            if (_reserva is not null)
            {
                _reserva.Recriar -= AoRecriar;
                _reserva.TelaCheia -= AoMudarTelaCheia;
                _reserva.Dispose();
                _reserva = null;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleBarra.Fechar", e); }
    }
}

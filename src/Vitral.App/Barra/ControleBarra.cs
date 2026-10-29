using System.ComponentModel;
using System.Windows;
using Vitral.Core.Barra;
using Vitral.Core.Perfis;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// A barra do topo de verdade: a reserva, as três ilhas e as fontes de dados. Nunca lança (contrato de
/// <see cref="IControleBarra"/>): erro vai para o log e, se a reserva falhar, as ilhas aparecem mesmo assim com um aviso.
internal sealed class ControleBarra(Action<string> avisar, IFonteBandeja bandeja) : IControleBarra
{
    private ReservaFaixa? _reserva;
    private Fontes? _fontes;
    private JanelaIlha? _midia, _status;
    private JanelaNotch? _relogio; // o notch vivo (spec do notch vivo §3)
    private JanelaFundoBarra? _fundo; // barra única (spec das formas §5.2)
    private IlhaMidiaViewModel? _vmMidia;
    private IlhaRelogioViewModel? _vmRelogio;
    private IlhaStatusViewModel? _vmStatus;
    private BarraTopoPerfil _estado = BarraTopoPerfil.Desligada;

    // Painéis próprios (spec v2): um por vez, decidido pelo coordenador; as janelas nascem na primeira abertura.
    private readonly CoordenadorPaineis _coordenador = new();
    private readonly PassarMouse _passar = new(() => DateTime.Now);
    private readonly System.Windows.Threading.DispatcherTimer _tiquePassar = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<Painel, JanelaPainel> _paineis = [];
    private CalendarioViewModel? _vmCalendario;

    /// A agenda (spec da agenda): o calendário mostra os compromissos e o notch avisa. Nula = só o mês.
    public Agenda.ServicoAgendaApp? Agenda { get; set; }
    private PainelBateriaViewModel? _vmBateria;
    private PainelWifiViewModel? _vmWifi;
    private readonly List<IDisposable> _vmsPaineis = [];
    private bool _coordenadorLigado;
    private IlhaStatus? _conteudoStatus;

    // SizeChanged e DpiChanged das ilhas e do notch reposicionam fora do evento e juntados (ver ExecucaoAgrupada).
    private ExecucaoAgrupada? _posicionarAgrupado, _reajustarAgrupado;
    private ExecucaoAgrupada PosicionarAgrupado => _posicionarAgrupado ??= new("ControleBarra.Posicionar", Posicionar, Application.Current.Dispatcher);
    private ExecucaoAgrupada ReajustarAgrupado => _reajustarAgrupado ??= new("ControleBarra.Reajustar", Reajustar, Application.Current.Dispatcher);

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
            _reserva = new ReservaFaixa(embaixo: false, () => Tema.ServicoFormas.Instancia.Atual.ReservaTopo);
            _reserva.Recriar += AoRecriar;
            _reserva.TelaCheia += AoMudarTelaCheia;
            Tema.ServicoFormas.Instancia.Mudou += Reajustar; // outra forma: reserva e posições de novo
        }
        if (!_reserva.Registrar()) avisar("Não foi possível reservar o espaço da barra do topo.");
        _fontes ??= new Fontes();
        if (!_coordenadorLigado)
        {
            _coordenador.PropertyChanged += AoMudarPainel;
            _coordenador.Fixou += AoFixarPainel;
            _tiquePassar.Tick += (_, _) => TiquePassar();
            _coordenadorLigado = true;
            bandeja.Mudou += AoMudarBandeja;
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
        _relogio = AjustarNotch(_relogio, _vmRelogio);
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
            if (conteudo is IlhaStatus status) { _conteudoStatus = status; status.MostrarSetinha(bandeja.Disponivel); }
            if (conteudo is IlhaMidia midia) midia.MouseNoTitulo += dentro => MouseNaMidia(painel: false, dentro);
            janela = new JanelaIlha(nome, presaNaBorda: nome == "Relógio") { Conteudo = conteudo };
            janela.SizeChanged += (_, _) => PosicionarAgrupado.Pedir();
            janela.DpiChanged += (_, _) => ReajustarAgrupado.Pedir(); // troca de escala nem sempre manda WM_DISPLAYCHANGE
        }
        janela.DataContext = vm;
        return janela;
    }

    /// O relógio vive dentro do notch vivo; o notch pede o calendário e avisa quando a largura do relógio muda.
    private JanelaNotch? AjustarNotch(JanelaNotch? notch, object? vm)
    {
        if (vm is null)
        {
            notch?.Close();
            return null;
        }
        if (notch is null)
        {
            notch = new JanelaNotch(new IlhaRelogio());
            notch.Pediu += p => _coordenador.Alternar(p, DateTime.Now);
            notch.PediuReiniciar += () => Reiniciar?.Invoke();
            notch.CalendarioNoDia += dia => _vmCalendario?.AbrirDia(dia); // o aviso da agenda abre o calendário no dia
            notch.LarguraNormalMudou += Posicionar;
            notch.SizeChanged += (_, _) => PosicionarAgrupado.Pedir(); // a HWND pode mudar de tamanho ao trocar de escala
            // Depois de o WPF terminar a troca de escala dele, que deixa a HWND no tamanho antigo (PosicaoNativa.Colocar)
            notch.DpiChanged += (_, _) => ReajustarAgrupado.Pedir();
            var esse = notch;
            esse.IsVisibleChanged += (_, e) =>
            {
                if (e.NewValue is not true || _atualizacaoGuardada is not { } guardado) return;
                _atualizacaoGuardada = null;
                esse.Avisar(guardado);
            };
        }
        notch.DataContext = vm;
        return notch;
    }

    /// Pedido de permissão do Claude (spec das permissões §3.5): vai para a ficha do notch; sem notch visível, sem resposta.
    public Task<Vitral.Core.Claude.EscolhaPermissao?> Pedir(Vitral.Core.Claude.PedidoPermissao pedido)
    {
        return _relogio is { IsVisible: true } notch ? notch.Pedir(pedido) : Task.FromResult<Vitral.Core.Claude.EscolhaPermissao?>(null);
    }

    /// Uma sessão terminou fora do app (spec da resposta §4.3): vai para a ficha de resposta; sem notch visível, vai
    /// direto para as pendências e a sessão é solta na hora.
    public Task<string?> Responder(Vitral.Core.Claude.PedidoResposta pedido)
    {
        if (_relogio is { IsVisible: true } notch) return notch.Responder(pedido);
        _relogio?.SemNotch(pedido);
        return Task.FromResult<string?>(null);
    }

    /// O Claude largou o pedido (respondido no app): a ficha sai.
    public void Desistir(string id) => _relogio?.Desistir(id);

    /// O clique no aviso de atualização (o App liga ao ServicoAtualizacao).
    public Action? Reiniciar { get; set; }

    /// Aviso do Claude chegou pelo canal: vai para o notch; sem a barra do topo (ou sem o relógio), é descartado. O de
    /// versão nova, que só vem uma vez, espera o notch voltar (tela cheia, barra desligada).
    public void Avisar(Vitral.Core.Claude.AvisoClaude aviso)
    {
        if (_relogio is { IsVisible: true } notch) notch.Avisar(aviso);
        else if (aviso.Tipo == Vitral.Core.Claude.TipoAviso.Atualizacao) _atualizacaoGuardada = aviso;
    }

    /// Um compromisso entrou na antecedência (spec da agenda §5.5). Com o notch escondido (tela cheia), o estado guarda
    /// o aviso e ele vira lembrete; sem a barra do topo, não há onde avisar.
    public void Lembrar(Vitral.Ui.Barra.AvisoAgenda aviso) => _relogio?.Lembrar(aviso);

    public void SumiuDaAgenda(string idCompromisso) => _relogio?.SumiuDaAgenda(idCompromisso);

    private Vitral.Core.Claude.AvisoClaude? _atualizacaoGuardada;

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
        bool fundoNovo = _fundo is not null && !_fundo.IsVisible && !_telaCheia;
        Mostrar(_fundo, !_telaCheia); // primeiro o fundo, para as ilhas e o notch ficarem na frente dele
        Mostrar(_midia, !_telaCheia && _vmMidia?.Visivel == true);
        Mostrar(_relogio, !_telaCheia);
        Mostrar(_status, !_telaCheia);
        if (fundoNovo) TrazerIlhasParaFrente();
    }

    private static void Mostrar(Window? janela, bool visivel)
    {
        if (janela is null) return;
        if (visivel && !janela.IsVisible) janela.Show();
        else if (!visivel && janela.IsVisible) janela.Hide();
    }

    /// A doca ligou ou desligou a bandeja do Vitral: a setinha acompanha; sem bandeja, o painel de ícones fecha.
    private void AoMudarBandeja()
    {
        _conteudoStatus?.MostrarSetinha(bandeja.Disponivel);
        if (!bandeja.Disponivel && _coordenador.Aberto == Painel.Icones) _coordenador.FecharTodos();
    }

    private void AoMudarPainel(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CoordenadorPaineis.Aberto)) return;
        try
        {
            var aberto = _coordenador.Aberto;
            if (aberto != Painel.Midia) _passar.Fechou(); // fechou ou foi trocado: o passar larga a mídia
            foreach (var (painel, janela) in _paineis)
                if (painel != aberto && janela.IsVisible) janela.Hide();
            if (aberto == Painel.Nenhum) return;

            var janelaPainel = JanelaDoPainel(aberto);
            Window? ilha = aberto switch { Painel.Calendario or Painel.Pendencias => _relogio, Painel.Midia => _midia, _ => _status };
            if (janelaPainel is null || ilha is null)
            {
                _coordenador.FecharTodos(); // painel ainda não existe nesta versão
                return;
            }
            var alinhamento = aberto switch
            {
                Painel.Calendario or Painel.Pendencias => AlinhamentoPainel.Centro,
                Painel.Midia => AlinhamentoPainel.Esquerda,
                _ => AlinhamentoPainel.Direita,
            };
            var p = MonitorPrincipal.Ler();
            var monitor = PosicaoIlhas.ParaDips(p.X, p.Y, p.Largura, p.Altura, p.Dpi);
            var ret = ilha is JanelaNotch notch ? notch.AreaDoRelogio(p.Dpi) : PosicaoNativa.Ler(ilha, p.Dpi);
            janelaPainel.Mostrar(PosicaoIlhas.Painel(ret, janelaPainel.Width, janelaPainel.ActualHeight, alinhamento, monitor),
                p.Dpi, ativar: !_coordenador.SemFoco);
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
            if (painel == Painel.Calendario) { _vmCalendario?.Reabrir(); if (Agenda is not null) _ = Agenda.AtualizarAgora(); }
            if (painel == Painel.Bateria) _vmBateria?.Abrir();
            if (painel == Painel.Wifi) _ = _vmWifi?.AbrirAsync();
            return existente;
        }
        JanelaPainel? nova = null;
        switch (painel)
        {
            case Painel.Calendario when _fontes is not null:
                _vmCalendario = Guardar(new CalendarioViewModel(_fontes.Agora, Agenda?.Agenda));
                if (Agenda is not null) _ = Agenda.AtualizarAgora(); // ao abrir o painel (spec da agenda §5.2)
                nova = new JanelaPainel("Calendário", 280) { Conteudo = new PainelCalendario(), DataContext = _vmCalendario };
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
            case Painel.Pendencias when _relogio is not null:
                var lista = new PainelPendencias(_relogio.Pendencias);
                lista.Vazia += () => { if (_coordenador.Aberto == Painel.Pendencias) _coordenador.FecharTodos(); };
                nova = new JanelaPainel("Pendências", 460) { Conteudo = lista };
                break;
            case Painel.Icones:
                nova = new JanelaPainel("Ícones", 280) { Conteudo = new PainelIcones(), DataContext = Guardar(new PainelIconesViewModel(bandeja)) };
                break;
            case Painel.Midia when _fontes is not null:
                nova = new JanelaPainel("Mídia", 260)
                {
                    Conteudo = new PainelMidia(),
                    DataContext = Guardar(new PainelMidiaViewModel(_fontes.Midia, new RelogioWpf(), _fontes.Agora)),
                };
                nova.MouseEnter += (_, _) => MouseNaMidia(painel: true, dentro: true);
                nova.MouseLeave += (_, _) => MouseNaMidia(painel: true, dentro: false);
                nova.Activated += (_, _) => _coordenador.Fixar(); // clicou dentro do painel aberto pelo mouse
                break;
        }
        if (nova is null) return null;
        nova.PerdeuFoco += () => _coordenador.FecharPorFoco(painel, DateTime.Now);
        nova.PediuFechar += _coordenador.FecharTodos;
        _paineis[painel] = nova;
        return nova;
    }

    /// O mouse na capa e no título da mídia, ou no painel dela (ver <see cref="PassarMouse"/>).
    private void MouseNaMidia(bool painel, bool dentro)
    {
        if (dentro) _passar.Entrou(painel); else _passar.Saiu(painel);
        if (!_tiquePassar.IsEnabled && !_passar.Ocioso) _tiquePassar.Start();
    }

    private void TiquePassar()
    {
        switch (_passar.Tique())
        {
            case AcaoPassar.Abrir:
                if (!_coordenador.AbrirSemFoco(Painel.Midia)) _passar.Fechou(); // outro painel em uso: não toma o lugar
                break;
            case AcaoPassar.Fechar:
                _coordenador.FecharSemFoco(Painel.Midia);
                break;
        }
        if (_passar.Ocioso) _tiquePassar.Stop();
    }

    /// Fixado pelo clique (no título ou dentro do painel): o painel ganha o foco e passa a fechar como os outros.
    private void AoFixarPainel()
    {
        _passar.Fixou();
        if (_coordenador.Aberto is var p && _paineis.TryGetValue(p, out var janela) && janela.IsVisible && !janela.IsActive) janela.Activate();
    }

    private T Guardar<T>(T vm) where T : IDisposable
    {
        _vmsPaineis.Add(vm);
        return vm;
    }

    private void FecharPaineis()
    {
        _tiquePassar.Stop();
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
            var formas = Tema.ServicoFormas.Instancia.Atual;
            _midia?.AplicarCantosRetos(formas.Ilhas.CantosRetos);
            _status?.AplicarCantosRetos(formas.Ilhas.CantosRetos);
            bool unica = formas.Ilhas.Disposicao == Core.Perfis.DisposicaoIlhas.BarraUnica;
            _midia?.AplicarSemVidro(unica);
            _status?.AplicarSemVidro(unica);
            AjustarFundo(unica, PosicaoIlhas.Barra(monitor, formas.Ilhas), formas.Ilhas.CantosRetos, p.Dpi);
            var (m, r, s) = PosicaoIlhas.Calcular(monitor, _midia?.ActualWidth ?? 0, _relogio?.LarguraNormal ?? 0, _status?.ActualWidth ?? 0,
                formas.Ilhas, formas.Notch);
            if (_midia is not null) PosicaoNativa.Colocar(_midia, m.X, m.Y, p.Dpi);
            _relogio?.Colocar(r, p.Dpi);
            if (_status is not null) PosicaoNativa.Colocar(_status, s.X + s.Largura, s.Y, p.Dpi, ancora: 1);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleBarra.Posicionar", e); }
    }

    /// Barra única: o fundo de vidro de lado a lado, criado na primeira vez e fechado ao voltar para ilhas separadas.
    private void AjustarFundo(bool unica, Retangulo barra, bool cantosRetos, uint dpi)
    {
        if (!unica)
        {
            _fundo?.Close();
            _fundo = null;
            return;
        }
        _fundo ??= new JanelaFundoBarra();
        _fundo.AplicarCantosRetos(cantosRetos);
        PosicaoNativa.Colocar(_fundo, barra.X, barra.Y, dpi, tamanho: new Size(barra.Largura, barra.Altura));
        AtualizarVisibilidadeMidia();
    }

    private void TrazerIlhasParaFrente()
    {
        foreach (var janela in new Window?[] { _midia, _status, _relogio })
            if (janela?.IsVisible == true) PosicaoNativa.TrazerParaFrente(janela);
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
            _fundo?.Close();
            _fundo = null;
            _status?.Close();
            _midia = _status = null;
            _relogio = null;
            DescartarViewModels();
            _fontes?.Dispose();
            _fontes = null;
            _telaCheia = false;
            if (_reserva is not null)
            {
                _reserva.Recriar -= AoRecriar;
                _reserva.TelaCheia -= AoMudarTelaCheia;
                Tema.ServicoFormas.Instancia.Mudou -= Reajustar;
                _reserva.Dispose();
                _reserva = null;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleBarra.Fechar", e); }
    }
}

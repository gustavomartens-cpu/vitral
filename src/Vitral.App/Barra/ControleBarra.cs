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

    /// As fontes vivem enquanto a barra está ligada (COM e WinRT são soltos ao desligar).
    private sealed class Fontes : IDisposable
    {
        public FonteMidiaWindows Midia { get; } = new(Application.Current.Dispatcher);
        public FonteVolumeWindows Volume { get; } = new(Application.Current.Dispatcher);
        public FonteRedeWindows Rede { get; } = new(Application.Current.Dispatcher);
        public FonteBateriaWindows Bateria { get; } = new(Application.Current.Dispatcher);
        public FonteDesempenhoWindows Desempenho { get; } = new();
        public RelogioDoSistema Agora { get; } = new();

        public void Dispose()
        {
            Midia.Dispose();
            Volume.Dispose();
            Rede.Dispose();
            Bateria.Dispose();
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
            janela = new JanelaIlha(nome) { Conteudo = criar() };
            janela.SizeChanged += (_, _) => Posicionar();
            janela.DpiChanged += (_, _) => Reajustar(); // troca de escala nem sempre manda WM_DISPLAYCHANGE
        }
        janela.DataContext = vm;
        return janela;
    }

    private void AoMudarMidia(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IlhaMidiaViewModel.Visivel)) AtualizarVisibilidadeMidia();
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

    private void AoMudarTelaCheia(bool telaCheia)
    {
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

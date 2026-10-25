using System.Windows;
using System.Windows.Controls;
using Vitral.App.Barra;
using Vitral.Core.Barra;
using Vitral.Core.Perfis;
using Vitral.Ui.Barra;

namespace Vitral.App.Doca;

/// A doca de verdade: a reserva de 62 px embaixo e a janela centralizada no monitor principal.
/// Nunca lança (contrato de <see cref="IControleDoca"/>): erro vai para o log.
internal sealed class ControleDoca(BarraDoWindows barraWindows, FonteBandeja bandeja, Action<string> avisar) : IControleDoca
{
    private ReservaFaixa? _reserva;
    private JanelaDoca? _janela;
    private bool _telaCheia;

    public void Aplicar(BarraBaixoPerfil estado)
    {
        try
        {
            if (estado.Visivel) Abrir();
            else Fechar();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("ControleDoca.Aplicar", e);
            avisar("A doca não pôde ser ajustada. Detalhes no log.");
        }
    }

    private void Abrir()
    {
        if (_reserva is null)
        {
            _reserva = new ReservaFaixa(embaixo: true, PosicaoIlhas.ReservaDoca);
            _reserva.Recriar += AoRecriar;
            _reserva.TelaCheia += AoMudarTelaCheia;
        }
        if (!_reserva.Registrar()) avisar("Não foi possível reservar o espaço da doca.");
        if (_janela is null)
        {
            _janela = new JanelaDoca { Conteudo = ConteudoInicial() };
            _janela.SizeChanged += (_, _) => Posicionar();
            _janela.DpiChanged += (_, _) => Reajustar();
        }
        bandeja.Ligar(); // o Vitral assume a bandeja enquanto a barra do Windows está escondida
        if (!_telaCheia) _janela.Show();
        Posicionar();
    }

    /// Conteúdo provisório (Task 2): só o logo; os apps entram na Task 4.
    private static FrameworkElement ConteudoInicial() =>
        new Image { Source = (System.Windows.Media.ImageSource)Application.Current.Resources["Logo"], Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center };

    public void Posicionar()
    {
        if (_janela is null) return;
        try
        {
            var p = MonitorPrincipal.Ler();
            var monitor = PosicaoIlhas.ParaDips(p.X, p.Y, p.Largura, p.Altura, p.Dpi);
            var r = PosicaoIlhas.Doca(monitor, _janela.ActualWidth);
            _janela.Left = r.X;
            _janela.Top = r.Y;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleDoca.Posicionar", e); }
    }

    private void AoRecriar()
    {
        barraWindows.ReaplicarSeEscondida(); // o Explorer novo mostra a barra dele
        if (_reserva is not null && !_reserva.Registrar()) avisar("Não foi possível reservar o espaço da doca.");
        Posicionar();
    }

    /// Monitores, escala ou volta da suspensão.
    public void Reajustar()
    {
        if (_reserva is null) return;
        barraWindows.ReaplicarSeEscondida();
        _reserva.Reposicionar();
        Posicionar();
    }

    private void AoMudarTelaCheia(bool telaCheia)
    {
        _telaCheia = telaCheia;
        if (_janela is null) return;
        if (telaCheia) _janela.Hide();
        else { _janela.Show(); Posicionar(); }
    }

    private void Fechar()
    {
        bandeja.Desligar();
        _janela?.Close();
        _janela = null;
        _telaCheia = false;
        if (_reserva is null) return;
        _reserva.Recriar -= AoRecriar;
        _reserva.TelaCheia -= AoMudarTelaCheia;
        _reserva.Dispose();
        _reserva = null;
    }
}

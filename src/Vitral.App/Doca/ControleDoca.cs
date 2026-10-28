using System.Windows;
using System.Windows.Controls;
using Vitral.App.Barra;
using Vitral.Core.Barra;
using Vitral.Core.Doca;
using Vitral.Core.Perfis;
using Vitral.Core.Windows;
using Vitral.Ui.Barra;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

/// A doca de verdade: a reserva de 62 px embaixo e a janela centralizada no monitor principal.
/// Nunca lança (contrato de <see cref="IControleDoca"/>): erro vai para o log.
internal sealed class ControleDoca(BarraDoWindows barraWindows, FonteBandeja bandeja, Action<string> avisar) : IControleDoca
{
    private ReservaFaixa? _reserva;
    private JanelaDoca? _janela;
    private bool _telaCheia;
    private FonteJanelas? _fonteJanelas;
    private DocaViewModel? _vm;

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
            _reserva = new ReservaFaixa(embaixo: true, () => Tema.ServicoFormas.Instancia.Atual.Doca.Reserva);
            _reserva.Recriar += AoRecriar;
            _reserva.TelaCheia += AoMudarTelaCheia;
            Tema.ServicoFormas.Instancia.Mudou += Reajustar; // outra forma: reserva, tamanho e posição de novo
        }
        if (!_reserva.Registrar()) avisar("Não foi possível reservar o espaço da doca.");
        if (_janela is null)
        {
            _janela = new JanelaDoca { Conteudo = new ConteudoDoca() };
            _janela.SizeChanged += (_, _) => Posicionar();
            _janela.DpiChanged += (_, _) => Reajustar();
        }
        bandeja.Ligar(); // o Vitral assume a bandeja enquanto a barra do Windows está escondida
        if (_vm is null)
        {
            // As janelas vêm do mesmo ShellManager da bandeja; os fixados, do fixados.json (importado do Windows na 1ª vez).
            _fonteJanelas = new FonteJanelas(bandeja, _janela.Dispatcher);
            _vm = new DocaViewModel(_fonteJanelas, new FonteFixados(new RepositorioFixados(System.IO.Path.Combine(Pastas.Raiz, "fixados.json"))), new AtalhosDoca(), new DialogosWpf());
            ((FrameworkElement)_janela.Conteudo!).DataContext = _vm;
        }
        if (!_telaCheia) _janela.Show();
        Posicionar();
    }

    public void Posicionar()
    {
        if (_janela is null) return;
        try
        {
            var p = MonitorPrincipal.Ler();
            var monitor = PosicaoIlhas.ParaDips(p.X, p.Y, p.Largura, p.Altura, p.Dpi);
            var formas = Tema.ServicoFormas.Instancia.Atual.Doca;
            _janela.AplicarFormas(formas, monitor.Largura);
            _janela.UpdateLayout(); // a largura nova (corrida ou de volta ao conteúdo) antes de calcular a posição
            var r = PosicaoIlhas.Doca(monitor, _janela.ActualWidth, formas);
            PosicaoNativa.Colocar(_janela, r.X + r.Largura / 2, r.Y, p.Dpi, ancora: 0.5);
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
        _vm?.Dispose();
        _vm = null;
        _fonteJanelas?.Dispose();
        _fonteJanelas = null;
        bandeja.Desligar();
        _janela?.Close();
        _janela = null;
        _telaCheia = false;
        if (_reserva is null) return;
        _reserva.Recriar -= AoRecriar;
        _reserva.TelaCheia -= AoMudarTelaCheia;
        Tema.ServicoFormas.Instancia.Mudou -= Reajustar;
        _reserva.Dispose();
        _reserva = null;
    }
}

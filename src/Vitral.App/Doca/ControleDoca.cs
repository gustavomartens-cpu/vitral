using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Vitral.App.Barra;
using Vitral.Core.Barra;
using Vitral.Core.Doca;
using Vitral.Core.Perfis;
using Vitral.Core.Windows;
using Vitral.Ui.Barra;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

/// A doca de verdade: a reserva de 62 px embaixo e a janela centralizada no monitor principal. Com "esconder
/// automaticamente" (spec de 17/11/2026), sem reserva: a doca desce para fora da tela e volta pela borda de baixo.
/// Nunca lança (contrato de <see cref="IControleDoca"/>): erro vai para o log.
internal sealed partial class ControleDoca(BarraDoWindows barraWindows, FonteBandeja bandeja, Action<string> avisar) : IControleDoca
{
    [StructLayout(LayoutKind.Sequential)] private struct Ponto { public int X, Y; }
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetCursorPos(out Ponto p);

    private static readonly TimeSpan DuracaoDeslize = TimeSpan.FromMilliseconds(150);

    private ReservaFaixa? _reserva;
    private JanelaDoca? _janela;
    private bool _telaCheia;
    private FonteJanelas? _fonteJanelas;
    private DocaViewModel? _vm;

    // Esconder automaticamente: a regra, a leitura do cursor e o deslize.
    private bool _esconder;
    private readonly AutoOcultarDoca _auto = new();
    private DispatcherTimer? _vigiaMouse, _quadros;
    // Última posição visível e o monitor (DIPs do principal), e quanto a doca está descida dela agora (0 = no lugar).
    private Retangulo _alvo, _monitor;
    private uint _dpi = 96;
    private double _descida, _descidaInicio, _descidaFim;
    private readonly Stopwatch _relogioDeslize = new();

    public void Aplicar(BarraBaixoPerfil estado)
    {
        try
        {
            if (estado.Visivel) Abrir(estado.EsconderAutomatico == true);
            else Fechar();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("ControleDoca.Aplicar", e);
            avisar("A doca não pôde ser ajustada. Detalhes no log.");
        }
    }

    private void Abrir(bool esconder)
    {
        if (_reserva is null)
        {
            _reserva = new ReservaFaixa(embaixo: true, () => Tema.ServicoFormas.Instancia.Atual.Doca.Reserva);
            _reserva.Recriar += AoRecriar;
            _reserva.TelaCheia += AoMudarTelaCheia;
            Tema.ServicoFormas.Instancia.Mudou += Reajustar; // outra forma: reserva, tamanho e posição de novo
        }
        bool mudouEsconder = esconder != _esconder;
        _esconder = esconder;
        if (_esconder) _reserva.Liberar(); // sem reserva: as janelas maximizadas vão até embaixo
        else if (!_reserva.Registrar()) avisar("Não foi possível reservar o espaço da doca.");
        if (_janela is null)
        {
            _janela = new JanelaDoca { Conteudo = new ConteudoDoca() };
            // Fora do evento e juntados: reposicionar dentro dele gerava outro, sem fim (ver ExecucaoAgrupada).
            var posicionar = new ExecucaoAgrupada("ControleDoca.Posicionar", Posicionar, _janela.Dispatcher);
            var reajustar = new ExecucaoAgrupada("ControleDoca.Reajustar", Reajustar, _janela.Dispatcher);
            // A 125% o WPF alterna a altura de 47 DIPs entre 46,4 e 47,2 (58 e 59 px); reposicionar a cada troca
            // provocava a seguinte. Só a largura muda a posição, e as formas mudam a altura de 1 DIP para cima.
            _janela.SizeChanged += (_, e) =>
            {
                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) >= 0.5 || Math.Abs(e.NewSize.Height - e.PreviousSize.Height) >= 1)
                    posicionar.Pedir();
            };
            _janela.DpiChanged += (_, _) => reajustar.Pedir();
        }
        bandeja.Ligar(); // o Vitral assume a bandeja enquanto a barra do Windows está escondida
        if (_vm is null)
        {
            // As janelas vêm do mesmo ShellManager da bandeja; os fixados, do fixados.json (importado do Windows na 1ª vez).
            _fonteJanelas = new FonteJanelas(bandeja, _janela.Dispatcher);
            _vm = new DocaViewModel(_fonteJanelas, new FonteFixados(new RepositorioFixados(System.IO.Path.Combine(Pastas.Raiz, "fixados.json"))), new AtalhosDoca(), new DialogosWpf());
            ((FrameworkElement)_janela.Conteudo!).DataContext = _vm;
        }
        if (_esconder) LigarEsconder(reiniciar: mudouEsconder || _vigiaMouse?.IsEnabled != true);
        else if (mudouEsconder) DesligarEsconder();
        if (!_telaCheia && (!_esconder || _auto.Visivel)) _janela.Show();
        Posicionar();
    }

    /// Ligar a opção com a doca à mostra: ela fica e começa a contar para sumir; em tela cheia, já começa escondida.
    private void LigarEsconder(bool reiniciar)
    {
        if (reiniciar) _auto.Reiniciar(DateTime.UtcNow, visivel: !_telaCheia);
        if (_vigiaMouse is null)
        {
            _vigiaMouse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _vigiaMouse.Tick += (_, _) => VigiarMouse();
        }
        _vigiaMouse.Start();
    }

    /// Desligar a opção: a doca volta ao lugar (o Abrir a mostra e reserva de novo).
    private void DesligarEsconder()
    {
        _vigiaMouse?.Stop();
        PararDeslize();
        _descida = 0;
        _auto.Reiniciar(DateTime.UtcNow, visivel: true);
    }

    /// A cada 50 ms: onde está o cursor (também durante um arraste de arquivo, que é do Windows) e o que a regra manda.
    private void VigiarMouse()
    {
        try
        {
            if (_janela is null || !GetCursorPos(out var c)) return;
            var p = MonitorPrincipal.Ler();
            var monitorPx = new Retangulo(p.X, p.Y, p.Largura, p.Altura);
            double e = p.Dpi / 96.0;
            var docaPx = new Retangulo(_alvo.X * e, _alvo.Y * e, _alvo.Largura * e, _alvo.Altura * e);
            bool travada = _janela.Conteudo is ConteudoDoca conteudo && conteudo.Ocupada;
            var acao = _auto.Conferir(DateTime.UtcNow, AutoOcultarDoca.NaBorda(c.X, c.Y, monitorPx),
                AutoOcultarDoca.SobreDoca(c.X, c.Y, docaPx, monitorPx), travada, _telaCheia);
            if (acao == AcaoDoca.Mostrar) Subir();
            else if (acao == AcaoDoca.Esconder) Descer();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleDoca.VigiarMouse", e); }
    }

    /// Quanto descer para a doca sair inteira da tela (DIPs): do topo dela até a base do monitor.
    private double DescidaTotal => Math.Max(0, _monitor.Y + _monitor.Altura - _alvo.Y);

    private void Subir()
    {
        if (_janela is null || _telaCheia) return;
        if (!_janela.IsVisible)
        {
            _descida = DescidaTotal;
            Colocar();
            _janela.Show();
        }
        PosicaoNativa.TrazerParaFrente(_janela); // por cima das outras janelas "sempre no topo"
        Deslizar(0);
    }

    private void Descer() => Deslizar(DescidaTotal);

    /// Desliza até a descida pedida (~150 ms, desacelerando); sem "Efeitos de animação" do Windows, vai direto.
    /// Na descida total, a janela se esconde no fim: nada invisível fica pegando cliques na borda.
    private void Deslizar(double fim)
    {
        if (_janela is null) return;
        if (!Tema.Movimento.Ligado || !_janela.IsVisible)
        {
            PararDeslize();
            _descida = fim;
            Colocar();
            AoFimDoDeslize();
            return;
        }
        (_descidaInicio, _descidaFim) = (_descida, fim);
        _relogioDeslize.Restart();
        if (_quadros is null)
        {
            _quadros = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
            _quadros.Tick += (_, _) => Quadro();
        }
        _quadros.Start();
    }

    private void Quadro()
    {
        double t = Math.Min(1, _relogioDeslize.Elapsed / DuracaoDeslize);
        double suave = 1 - Math.Pow(1 - t, 3);
        _descida = _descidaInicio + (_descidaFim - _descidaInicio) * suave;
        try { Colocar(); }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleDoca.Quadro", e); }
        if (t < 1) return;
        PararDeslize();
        AoFimDoDeslize();
    }

    private void PararDeslize()
    {
        _quadros?.Stop();
        _relogioDeslize.Reset();
    }

    private void AoFimDoDeslize()
    {
        if (_janela is not null && _esconder && !_auto.Visivel) _janela.Hide();
    }

    private void Colocar()
    {
        if (_janela is null) return;
        PosicaoNativa.Colocar(_janela, _alvo.X + _alvo.Largura / 2, _alvo.Y + _descida, _dpi, ancora: 0.5);
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
            (_alvo, _monitor, _dpi) = (PosicaoIlhas.Doca(monitor, _janela.ActualWidth, formas), monitor, p.Dpi);
            // Escondida e parada, fica descida inteira (a forma ou o monitor podem ter mudado a altura).
            if (_esconder && !_auto.Visivel && _quadros?.IsEnabled != true) _descida = DescidaTotal;
            Colocar();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ControleDoca.Posicionar", e); }
    }

    private void AoRecriar()
    {
        barraWindows.ReaplicarSeEscondida(); // o Explorer novo mostra a barra dele
        if (_reserva is not null && !_esconder && !_reserva.Registrar()) avisar("Não foi possível reservar o espaço da doca.");
        Posicionar();
    }

    /// Monitores, escala ou volta da suspensão.
    public void Reajustar()
    {
        if (_reserva is null) return;
        barraWindows.ReaplicarSeEscondida();
        if (!_esconder) _reserva.Reposicionar();
        Posicionar();
    }

    private void AoMudarTelaCheia(bool telaCheia)
    {
        _telaCheia = telaCheia;
        if (_janela is null) return;
        if (telaCheia) { PararDeslize(); _janela.Hide(); }
        else if (_esconder)
        {
            // Volta escondida: aparece de novo só pela borda.
            _auto.Reiniciar(DateTime.UtcNow, visivel: false);
            Posicionar();
        }
        else { _janela.Show(); Posicionar(); }
    }

    private void Fechar()
    {
        _vigiaMouse?.Stop();
        PararDeslize();
        _esconder = false;
        _descida = 0;
        _auto.Reiniciar(DateTime.UtcNow, visivel: true);
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

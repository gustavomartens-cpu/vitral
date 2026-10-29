using System.Windows.Threading;

namespace Vitral.App.Barra;

/// Roda uma ação uma vez quando a fila da interface esvazia, juntando os pedidos que chegam até lá, e no máximo
/// <see cref="LimitePorSegundo"/> vezes por segundo. Reposicionar dentro do SizeChanged/DpiChanged gerava outro evento
/// na hora: depois da suspensão a doca ficou alternando entre 58 e 59 px de altura, com a CPU alta e a fila de
/// mensagens cheia ("Não há cota suficiente", 29/10). Passando do limite, espera 1 s e anota no log.
internal sealed class ExecucaoAgrupada
{
    private const int LimitePorSegundo = 10;

    private readonly string _nome;
    private readonly Action _acao;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _depois;
    private bool _pendente;
    private int _noSegundo;
    private DateTime _inicioDoSegundo;
    private bool _anotou;

    public ExecucaoAgrupada(string nome, Action acao, Dispatcher dispatcher)
    {
        _nome = nome;
        _acao = acao;
        _dispatcher = dispatcher;
        _depois = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _depois.Tick += (_, _) => { _depois.Stop(); Executar(); };
    }

    public void Pedir()
    {
        if (_pendente) return;
        _pendente = true;
        _dispatcher.BeginInvoke(Executar, DispatcherPriority.Background);
    }

    private void Executar()
    {
        var agora = DateTime.UtcNow;
        if (agora - _inicioDoSegundo >= TimeSpan.FromSeconds(1)) (_inicioDoSegundo, _noSegundo) = (agora, 0);
        if (++_noSegundo > LimitePorSegundo)
        {
            if (!_anotou) LogBarra.Info($"{_nome}: mais de {LimitePorSegundo} vezes em 1 s; espero 1 s");
            _anotou = true;
            _depois.Start(); // continua pendente: os pedidos até lá viram esta execução
            return;
        }
        _anotou = false;
        _pendente = false;
        try { _acao(); }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro(_nome, e); }
    }
}

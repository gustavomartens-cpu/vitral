namespace Vitral.App;

/// Garante um Vitral só por sessão. A segunda instância pede à primeira que mostre a janela e sai.
internal sealed class InstanciaUnica : IDisposable
{
    private const string NomeMutex = @"Local\Vitral-InstanciaUnica";
    private const string NomeEvento = @"Local\Vitral-Mostrar";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _evento;
    private readonly RegisteredWaitHandle? _espera;

    public bool EhPrimeira { get; }

    public InstanciaUnica(Action aoPedirParaMostrar)
    {
        _mutex = new Mutex(initiallyOwned: true, NomeMutex, out bool criado);
        EhPrimeira = criado;
        _evento = new EventWaitHandle(false, EventResetMode.AutoReset, NomeEvento);
        if (EhPrimeira)
            _espera = ThreadPool.RegisterWaitForSingleObject(_evento, (_, _) => aoPedirParaMostrar(), null, Timeout.Infinite, executeOnlyOnce: false);
        else
            _evento.Set();
    }

    public void Dispose()
    {
        _espera?.Unregister(null);
        if (EhPrimeira) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _evento.Dispose();
    }
}

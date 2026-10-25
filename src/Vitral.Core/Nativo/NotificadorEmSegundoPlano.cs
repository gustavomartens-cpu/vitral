namespace Vitral.Core.Nativo;

/// Envia os avisos ao Windows numa fila em segundo plano, sem bloquear quem chamou.
/// Motivo: o aviso vai a todas as janelas, inclusive a do próprio app. Enviado da thread da interface,
/// a janela do Vitral o recebia no meio do clique e o tema do WPF entrava em laço
/// ("referência cíclica ... propriedade Style"), derrubando o app. Fora da thread, ela o recebe depois, pelo caminho normal.
public sealed class NotificadorEmSegundoPlano(INotificador interno) : INotificador
{
    private readonly object _trava = new();
    private Task _ultima = Task.CompletedTask;

    public void AvisarMudancaDeTema() => Enfileirar(interno.AvisarMudancaDeTema);

    public void AvisarMudancaDaBarra() => Enfileirar(interno.AvisarMudancaDaBarra);

    public void AvisarOcultarAutomatico(bool ligado) => Enfileirar(() => interno.AvisarOcultarAutomatico(ligado));

    /// Espera os avisos pendentes (para testes e para sair do app sem perder um aviso).
    public bool Aguardar(TimeSpan prazo)
    {
        Task t;
        lock (_trava) t = _ultima;
        return t.Wait(prazo);
    }

    private void Enfileirar(Action aviso)
    {
        lock (_trava)
            _ultima = _ultima.ContinueWith(_ => aviso(), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default);
    }
}

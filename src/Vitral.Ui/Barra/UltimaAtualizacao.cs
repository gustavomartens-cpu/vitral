namespace Vitral.Ui.Barra;

/// Para leituras assíncronas que podem terminar fora de ordem: só a última que começou pode publicar o resultado.
/// Seguro entre threads.
public sealed class UltimaAtualizacao
{
    private int _atual;

    public int Comecar() => Interlocked.Increment(ref _atual);

    public bool EhAtual(int geracao) => geracao == Volatile.Read(ref _atual);
}

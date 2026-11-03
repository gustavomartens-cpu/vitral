namespace Vitral.Core.Controles;

/// Escreve fora da thread da interface, um de cada vez por chave, e só o último valor pedido enquanto uma escrita
/// estava em andamento (o brilho pelo DDC/CI é lento: escritas sobrepostas chegam fora de ordem). Falha na escrita
/// vai para <paramref name="aoFalhar"/> e não trava a fila.
public sealed class FilaUltimoValor<TChave, TValor>(Action<TChave, TValor> escrever, Action<Exception>? aoFalhar = null)
    where TChave : notnull
{
    private readonly object _trava = new();
    private readonly Dictionary<TChave, TValor> _pendentes = [];
    private readonly Dictionary<TChave, Task> _rodando = [];

    public void Pedir(TChave chave, TValor valor)
    {
        lock (_trava)
        {
            _pendentes[chave] = valor;
            if (!_rodando.ContainsKey(chave)) _rodando[chave] = Task.Run(() => Laco(chave));
        }
    }

    /// Termina quando todas as escritas pedidas até agora acabaram (para os testes).
    public Task Esperar()
    {
        lock (_trava) return Task.WhenAll(_rodando.Values.ToList());
    }

    private void Laco(TChave chave)
    {
        while (true)
        {
            TValor valor;
            lock (_trava)
            {
                if (!_pendentes.Remove(chave, out valor!))
                {
                    _rodando.Remove(chave);
                    return;
                }
            }
            try { escrever(chave, valor); }
            catch (Exception e) when (e is not OutOfMemoryException) { aoFalhar?.Invoke(e); }
        }
    }
}

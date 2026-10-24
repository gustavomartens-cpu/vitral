namespace Vitral.Cli;

/// Pergunta "manter?" com prazo. Só "s" confirma. Entrada encerrada (sem teclado ligado) conta como
/// "ainda sem resposta": espera o prazo inteiro e não confirma. Cancelamento (Ctrl+C) volta na hora sem confirmar.
public static class Confirmacao
{
    public static bool Esperar(Func<string?> lerLinha, TimeSpan prazo, CancellationToken cancelar = default)
    {
        if (cancelar.IsCancellationRequested) return false;
        var leitura = Task.Run(lerLinha);
        try
        {
            if (!leitura.Wait(prazo, cancelar)) return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        string? resposta = leitura.Result;
        if (resposta is null)
        {
            cancelar.WaitHandle.WaitOne(prazo); // entrada encerrada: respeita o prazo, mas atende Ctrl+C
            return false;
        }
        return resposta.Trim().Equals("s", StringComparison.OrdinalIgnoreCase);
    }
}

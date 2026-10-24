namespace Vitral.Cli;

/// Pergunta "manter?" com prazo. Só "s" confirma. Entrada encerrada (sem teclado ligado) conta como
/// "ainda sem resposta": espera o prazo inteiro e não confirma, para o teste de 20 s durar 20 s.
public static class Confirmacao
{
    public static bool Esperar(Func<string?> lerLinha, TimeSpan prazo)
    {
        var leitura = Task.Run(lerLinha);
        if (!leitura.Wait(prazo)) return false;
        string? resposta = leitura.Result;
        if (resposta is null)
        {
            Thread.Sleep(prazo); // entrada encerrada: nunca virá resposta; respeita o prazo
            return false;
        }
        return resposta.Trim().Equals("s", StringComparison.OrdinalIgnoreCase);
    }
}

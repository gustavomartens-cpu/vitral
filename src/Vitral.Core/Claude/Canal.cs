using System.IO.Pipes;
using System.Text;

namespace Vitral.Core.Claude;

/// Canal dos avisos (spec do notch vivo §4.4): named pipe com o SID do usuário no nome; cada aviso é uma linha JSON.
public static class Canal
{
    public static string Nome(string sid) => $"Vitral-Avisos-{sid}";

    /// Lado do mensageiro: conecta, escreve a linha e sai. Qualquer falha (Vitral fechado, ocupado ou congelado sem ler)
    /// devolve false dentro de <paramref name="esperaMs"/> para conectar e outro tanto para escrever.
    public static bool Enviar(string nomeCanal, string linha, int esperaMs = 300)
    {
        try
        {
            using var cliente = new NamedPipeClientStream(".", nomeCanal, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            cliente.Connect(esperaMs);
            var bytes = new UTF8Encoding(false).GetBytes(linha + "\n");
            // A escrita espera o outro lado ler quando o buffer enche: com o Vitral congelado, desiste no prazo.
            var escrita = cliente.WriteAsync(bytes, 0, bytes.Length);
            return escrita.Wait(esperaMs) && escrita.IsCompletedSuccessfully;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException or InvalidOperationException or AggregateException)
        {
            return false;
        }
    }
}

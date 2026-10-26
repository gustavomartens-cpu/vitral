using System.IO.Pipes;
using System.Text;

namespace Vitral.Core.Claude;

/// Canal dos avisos (spec do notch vivo §4.4): named pipe com o SID do usuário no nome; cada aviso é uma linha JSON.
public static class Canal
{
    public static string Nome(string sid) => $"Vitral-Avisos-{sid}";

    /// Pedido de permissão (spec das permissões §3.5): conecta nos dois sentidos, escreve a linha e espera uma linha de
    /// resposta até <paramref name="esperaRespostaMs"/>. Qualquer falha ou prazo esgotado devolve null.
    public static string? Perguntar(string nomeCanal, string linha, int esperaRespostaMs, int esperaConexaoMs = 300)
    {
        try
        {
            using var cliente = new NamedPipeClientStream(".", nomeCanal, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            cliente.Connect(esperaConexaoMs);
            using var prazo = new CancellationTokenSource(esperaRespostaMs);
            var bytes = new UTF8Encoding(false).GetBytes(linha + "\n");
            cliente.WriteAsync(bytes, prazo.Token).AsTask().Wait(prazo.Token);
            var lidos = new byte[AvisoClaude.MaximoLinha * 4 + 1];
            int total = 0;
            while (total < lidos.Length)
            {
                int n = cliente.ReadAsync(lidos.AsMemory(total), prazo.Token).AsTask().Result;
                if (n == 0) break;
                total += n;
                if (Array.IndexOf(lidos, (byte)'\n', 0, total) >= 0) break;
            }
            var texto = Encoding.UTF8.GetString(lidos, 0, total);
            int fim = texto.IndexOf('\n');
            return fim >= 0 ? texto[..fim].TrimEnd('\r') : null; // sem "\n": a conexão caiu antes da resposta inteira
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException or InvalidOperationException
                                       or AggregateException or OperationCanceledException)
        {
            return null;
        }
    }

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

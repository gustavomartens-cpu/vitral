using System.IO.Pipes;
using System.Text;

namespace Vitral.Core.Claude;

/// Canal dos avisos (spec do notch vivo §4.4): named pipe com o SID do usuário no nome; cada aviso é uma linha JSON.
public static class Canal
{
    public static string Nome(string sid) => $"Vitral-Avisos-{sid}";

    /// Lado do mensageiro: conecta, escreve a linha e sai. Qualquer falha (Vitral fechado, ocupado) devolve false.
    public static bool Enviar(string nomeCanal, string linha, int esperaMs = 300)
    {
        try
        {
            using var cliente = new NamedPipeClientStream(".", nomeCanal, PipeDirection.Out, PipeOptions.None);
            cliente.Connect(esperaMs);
            var bytes = new UTF8Encoding(false).GetBytes(linha + "\n");
            cliente.Write(bytes, 0, bytes.Length);
            cliente.Flush();
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }
}

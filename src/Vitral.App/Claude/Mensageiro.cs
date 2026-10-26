using System.IO;
using System.Security.Principal;
using System.Text;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// "Vitral.exe --aviso" (spec do notch vivo §4.3): roda no gancho do Claude Code, antes do Velopack e do WPF. Lê o que o
/// Claude manda, entrega ao Vitral pelo canal e sai. Nunca falha para o Claude: toda exceção é engolida.
internal static class Mensageiro
{
    private const int Limite = 64 * 1024;

    public static void Executar()
    {
        try
        {
            var entrada = LerEntrada(TimeSpan.FromSeconds(2));
            if (AvisoClaude.DeGancho(entrada, LerFim) is not { } aviso) return;
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid is not null) Canal.Enviar(Canal.Nome(sid), aviso.ParaLinha());
        }
        catch (Exception) { /* o Claude segue, com ou sem aviso */ }
    }

    private static string LerEntrada(TimeSpan espera)
    {
        var leitura = Task.Run(() =>
        {
            using var leitor = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var buffer = new char[Limite];
            int total = 0, lidos;
            while (total < Limite && (lidos = leitor.Read(buffer, total, Limite - total)) > 0) total += lidos;
            return new string(buffer, 0, total);
        });
        return leitura.Wait(espera) ? leitura.Result : "";
    }

    /// Os últimos 64 KB da transcrição (o Claude continua escrevendo nela: abre compartilhado).
    private static string? LerFim(string caminho)
    {
        try
        {
            using var f = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (f.Length > Limite) f.Seek(-Limite, SeekOrigin.End);
            using var leitor = new StreamReader(f, Encoding.UTF8);
            return leitor.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
}

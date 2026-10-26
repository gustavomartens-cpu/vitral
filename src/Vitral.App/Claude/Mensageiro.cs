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

    /// Pedido de permissão espera a resposta do notch (spec das permissões §3.4); o resto só entrega e sai.
    public static bool EhPermissao(string entrada) => entrada.Contains("\"PermissionRequest\"", StringComparison.Ordinal);

    public static string LerEntrada() => LerEntrada(TimeSpan.FromSeconds(2));

    public static void Executar(string entrada)
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid is null) return;
            if (PedidoPermissao.DeGancho(entrada) is { } pedido)
            {
                // Sem resposta (tempo, Vitral fechado): não escreve nada e o Claude segue o pedido normal dele.
                var resposta = Canal.Perguntar(Canal.Nome(sid), pedido.ParaLinha(), 60_000);
                if (RespostaPermissao.DaLinha(resposta, pedido.Id) is not { } escolha) return;
                using var saida = Console.OpenStandardOutput();
                var bytes = new UTF8Encoding(false).GetBytes(RespostaPermissao.ParaClaude(escolha, entrada));
                saida.Write(bytes, 0, bytes.Length);
                saida.Flush();
                return;
            }
            if (AvisoClaude.DeGancho(entrada, LerFim) is { } aviso) Canal.Enviar(Canal.Nome(sid), aviso.ParaLinha());
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

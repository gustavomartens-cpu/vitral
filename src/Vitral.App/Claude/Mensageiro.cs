using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// "Vitral.exe --aviso" (spec do notch vivo §4.3): roda no gancho do Claude Code, antes do Velopack e do WPF. Lê o que o
/// Claude manda, entrega ao Vitral pelo canal e sai. Nunca falha para o Claude: toda exceção é engolida.
internal static partial class Mensageiro
{
    // A entrada inteira de um pedido de permissão pode ser grande (um Write com o arquivo todo): cortada, viraria JSON
    // inválido e o pedido não chegaria ao notch. Da transcrição bastam os últimos 256 KB (a resposta inteira do Claude).
    private const int LimiteEntrada = 8 * 1024 * 1024, Limite = 256 * 1024;

    [LibraryImport("user32.dll")] private static partial IntPtr GetForegroundWindow();
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(IntPtr h, out uint processo);

    /// Pedido de permissão espera a resposta do notch (spec das permissões §3.4); o resto só entrega e sai.
    public static bool EhPermissao(string entrada) => entrada.Contains("\"PermissionRequest\"", StringComparison.Ordinal);

    /// A janela da frente é do app do Claude (o Gustavo está lá: o Stop não segura a sessão).
    public static bool ClaudeNaFrente()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var id);
            using var p = Process.GetProcessById((int)id);
            return p.ProcessName.Equals("claude", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    private static readonly Lazy<bool> AVista = new(SessaoAVista);

    /// A conversa desta sessão está aberta numa janela do Claude que é a da frente em algum monitor (com várias
    /// sessões, as outras continuam avisando). Sem o id da conversa (sessão de terminal), ou sem conseguir ler os
    /// endereços da janela, vale a regra antiga: o app do Claude na frente.
    private static bool SessaoAVista()
    {
        var appId = Environment.GetEnvironmentVariable(ConversaClaude.VariavelDoApp);
        if (string.IsNullOrEmpty(appId)) return ClaudeNaFrente();
        try
        {
            var janelas = JanelasDoClaude.Listar();
            if (ConversaClaude.AVista(appId, janelas)) return true;
            bool leuAlguma = janelas.Any(j => j.Enderecos.Any(e => e.StartsWith("https://claude.ai/", StringComparison.OrdinalIgnoreCase)));
            return !leuAlguma && ClaudeNaFrente();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return ClaudeNaFrente(); }
    }

    /// Segura o Stop para a resposta pelo notch? Lido uma vez, no começo: se a conversa está à vista, quem é a sessão
    /// (pelas variáveis do Claude Code) e se o gancho já foi religado com o tempo da resposta.
    public static bool SegurarStop(string entrada) =>
        Segurar.NoStop(entrada, AVista.Value,
            Segurar.SessaoComGente(Environment.GetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT"), Environment.GetEnvironmentVariable("CLAUDE_CODE_SESSION_ATTENDED")),
            GanchoComTempo());

    /// O settings.json do Claude já tem o Stop com o tempo da resposta (a caixa foi religada depois da 0.9.0).
    private static bool GanchoComTempo()
    {
        try
        {
            var caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
            return GanchosClaude.EstaLigado(File.ReadAllText(caminho));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// Prazo total do "--aviso": permissão 65 s; Stop que espera a resposta 19,5 min; o resto 1,5 s.
    public static int PrazoMs(string entrada, bool segurar) =>
        EhPermissao(entrada) ? 65_000 : segurar ? 1_170_000 : 1_500;

    public static string LerEntrada() => LerEntrada(TimeSpan.FromSeconds(2));

    public static void Executar(string entrada, bool segurar)
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid is null) return;
            if (PedidoPermissao.DeGancho(entrada) is { } pedido)
            {
                // Sem resposta (tempo, Vitral fechado): não escreve nada e o Claude segue o pedido normal dele.
                var resposta = Canal.Perguntar(Canal.Nome(sid), pedido.ParaLinha(), Canal.EsperaPedidoMs);
                if (RespostaPermissao.DaLinha(resposta, pedido.Id) is not { } escolha) return;
                Escrever(RespostaPermissao.ParaClaude(escolha, entrada));
                return;
            }
            if (AvisoClaude.DeGancho(entrada, LerFim, foraDoApp: !AVista.Value) is not { } lido) return;
            // A conversa no app do Claude (herdada da sessão), para o clique no notch abrir exatamente ela.
            var aviso = lido with { AppId = Environment.GetEnvironmentVariable(ConversaClaude.VariavelDoApp) };
            // Fora do app, o Stop vira ficha de resposta e segura a sessão até o texto (spec da resposta §3.3); sem
            // texto (tempo, Esc, Vitral fechado), não escreve nada e a sessão termina normal.
            if (segurar && PedidoResposta.DeAviso(aviso, DateTime.Now) is { } ficha)
            {
                var linha = Canal.Perguntar(Canal.Nome(sid), ficha.ParaLinha(), Canal.EsperaRespostaMs);
                if (RespostaTexto.DaLinha(linha, ficha.Id) is { } texto) Escrever(RespostaTexto.ParaClaude(texto));
                return;
            }
            Canal.Enviar(Canal.Nome(sid), aviso.ParaLinha());
        }
        catch (Exception) { /* o Claude segue, com ou sem aviso */ }
    }

    private static void Escrever(string json)
    {
        using var saida = Console.OpenStandardOutput();
        var bytes = new UTF8Encoding(false).GetBytes(json);
        saida.Write(bytes, 0, bytes.Length);
        saida.Flush();
    }

    private static string LerEntrada(TimeSpan espera)
    {
        var leitura = Task.Run(() =>
        {
            using var leitor = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var texto = new StringBuilder();
            var buffer = new char[64 * 1024];
            int lidos;
            while (texto.Length < LimiteEntrada && (lidos = leitor.Read(buffer, 0, buffer.Length)) > 0) texto.Append(buffer, 0, lidos);
            return texto.ToString();
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

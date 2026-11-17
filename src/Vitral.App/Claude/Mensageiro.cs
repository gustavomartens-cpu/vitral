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
    // Num turno longo, a mensagem que o abriu (com a origem, R4) pode estar antes disso: aí lê até 4 MB.
    private const int LimiteEntrada = 8 * 1024 * 1024, Limite = 256 * 1024, LimiteTurno = 4 * 1024 * 1024;

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

    private const int PrazoDaLeituraMs = 400;
    private static bool? _aVista;

    /// <see cref="SessaoAVista"/> uma vez por gancho e com prazo: o Claude travado (ou uma conversa enorme) não pode
    /// segurar o Claude Code. Estourou o prazo, vale a regra antiga.
    private static bool AVista()
    {
        if (_aVista is { } visto) return visto;
        var leitura = Task.Run(SessaoAVista);
        _aVista = leitura.Wait(PrazoDaLeituraMs) ? leitura.Result : ClaudeNaFrente();
        return _aVista.Value;
    }

    /// Esta conversa, identificada pelo id dela, está aberta numa janela do Claude à vista. Diferente do <see cref="AVista"/>,
    /// nunca cai na regra antiga do "app do Claude na frente": sem o id ou sem ler as janelas, o notch avisa (avisar a
    /// mais é melhor que esconder a conversa de outra sessão).
    private static bool SessaoAVistaNoApp()
    {
        var appId = Environment.GetEnvironmentVariable(ConversaClaude.VariavelDoApp);
        if (string.IsNullOrEmpty(appId)) return false;
        var leitura = Task.Run(() => ConversaClaude.AVista(appId, JanelasDoClaude.Listar()));
        try { return leitura.Wait(PrazoDaLeituraMs) && leitura.Result; }
        catch (AggregateException) { return false; }
    }

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
    /// Só o Stop lê as janelas; o pedido de permissão não paga essa leitura.
    public static bool SegurarStop(string entrada) =>
        entrada.Contains("\"Stop\"", StringComparison.Ordinal) && Segurar.NoStop(entrada, AVista(),
            Segurar.SessaoComGente(Environment.GetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT"), Environment.GetEnvironmentVariable("CLAUDE_CODE_SESSION_ATTENDED")),
            GanchoComTempo(), turnoDoGustavo: LerAviso(entrada)?.DeOutraOrigem != true);

    private static (string Entrada, AvisoClaude? Aviso)? _lido;

    /// O aviso do Stop ou do Notification, com a transcrição lida uma vez só por gancho (o SegurarStop e o Executar
    /// usam o mesmo).
    private static AvisoClaude? LerAviso(string entrada)
    {
        if (_lido is { } l && ReferenceEquals(l.Entrada, entrada)) return l.Aviso;
        var aviso = AvisoClaude.DeGancho(entrada, c => LerFim(c, Limite), lerMais: c => LerFim(c, LimiteTurno));
        _lido = (entrada, aviso);
        return aviso;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UltimoUso { public uint Tamanho; public uint Quando; }

    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetLastInputInfo(ref UltimoUso info);

    /// Há quanto tempo não há teclado nem mouse nesta sessão do Windows (null se não deu para ler).
    private static TimeSpan? SemUso()
    {
        var info = new UltimoUso { Tamanho = (uint)Marshal.SizeOf<UltimoUso>() };
        if (!GetLastInputInfo(ref info)) return null;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Quando)); // as duas voltam a zero a cada 49,7 dias
    }

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

    /// Prazo total do "--aviso": pergunta do Claude 310 s; permissão 65 s; Stop que espera a resposta 19,5 min; o resto 1,5 s.
    public static int PrazoMs(string entrada, bool segurar) =>
        EhPermissao(entrada) ? (EhPergunta(entrada) ? 310_000 : 65_000) : segurar ? 1_170_000 : 1_500;

    /// Uma pergunta do Claude (spec das perguntas §4): espera mais que uma permissão.
    private static bool EhPergunta(string entrada) =>
        entrada.Contains($"\"tool_name\":\"{RegrasPerguntas.Ferramenta}\"", StringComparison.Ordinal)
        || entrada.Contains($"\"tool_name\": \"{RegrasPerguntas.Ferramenta}\"", StringComparison.Ordinal);

    public static string LerEntrada() => LerEntrada(TimeSpan.FromSeconds(2));

    public static void Executar(string entrada, bool segurar)
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid is null) return;
            if (PedidoPermissao.DeGancho(entrada) is { } lidoPedido)
            {
                // Com a conversa aberta e à vista, a permissão e a pergunta ficam só no app (Gustavo, 15/11/2026).
                if (SessaoAVistaNoApp()) return;
                // O Gustavo saiu da mesa (R5): o pedido não espera o notch e vai na hora para o app do Claude, que o
                // guarda até a volta (o "esperando você" do Claude vira pendência no notch).
                if (Ausencia.Ausente(SemUso())) return;
                // A conversa no app do Claude, para "Responder na conversa" abrir exatamente ela.
                var pedido = lidoPedido with { AppId = Environment.GetEnvironmentVariable(ConversaClaude.VariavelDoApp) };
                // Sem resposta (tempo, Vitral fechado): não escreve nada e o Claude segue o pedido normal dele.
                var espera = pedido.Ferramenta == RegrasPerguntas.Ferramenta ? Canal.EsperaPerguntaMs : Canal.EsperaPedidoMs;
                var resposta = Canal.Perguntar(Canal.Nome(sid), pedido.ParaLinha(), espera);
                // "Responder na conversa" também não escreve nada: a pergunta segue no app.
                if (RespostaPermissao.DaLinha(resposta, pedido.Id) is not { } escolha
                    || RespostaPermissao.ParaClaude(escolha, entrada) is not { } saida) return;
                Escrever(saida);
                return;
            }
            if (LerAviso(entrada) is not { } lido) return;
            // O começo do turno (R1) só conta a sessão como rodando: vai direto, sem ler as janelas.
            if (lido.Tipo == TipoAviso.Comecou) { Canal.Enviar(Canal.Nome(sid), lido.ParaLinha()); return; }
            // A conversa no app do Claude (herdada da sessão), para o clique no notch abrir exatamente ela. O fim de um
            // turno que o Gustavo não pediu (R4) segue pelo canal mesmo assim: tira do notch o pedido e a ficha da
            // sessão, que foram resolvidos, sem descer.
            var aviso = lido with { ForaDoApp = !AVista(), AppId = Environment.GetEnvironmentVariable(ConversaClaude.VariavelDoApp), AVista = SessaoAVistaNoApp() };
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

    /// Os últimos <paramref name="limite"/> bytes da transcrição (o Claude continua escrevendo nela: abre compartilhado).
    private static string? LerFim(string caminho, int limite)
    {
        try
        {
            using var f = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (f.Length > limite) f.Seek(-limite, SeekOrigin.End);
            using var leitor = new StreamReader(f, Encoding.UTF8);
            return leitor.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
}

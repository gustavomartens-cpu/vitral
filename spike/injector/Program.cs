// Injector da fase 0 (DESCARTÁVEL).
// Uso: Injector --rota <externa|interna> --alvo <explorer|iniciar> --modo <transparente|log> [--sem-host]
// Códigos de saída: 0 = Ready recebido; 2 = já anexado; 3 = outro consumidor rodando; 4 = timeout; 1 = erro de uso.
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vitral.Spike.Injector;

var clsid = new Guid("6B1C2E52-8F0A-4C3B-9A5E-3D7F1E2A9C41");

string Arg(string nome, string padrao)
{
    int i = Array.IndexOf(args, nome);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : padrao;
}

string rota = Arg("--rota", "");
string alvo = Arg("--alvo", "explorer");
string modo = Arg("--modo", "transparente");
bool semHost = args.Contains("--sem-host");
if (rota is not ("externa" or "interna") || alvo is not ("explorer" or "iniciar") || modo is not ("transparente" or "log"))
{
    Console.WriteLine("Uso: Injector --rota <externa|interna> --alvo <explorer|iniciar> --modo <transparente|log> [--sem-host]");
    return 1;
}

// Pastas: a DLL fica em spike\out; os resultados em spike\resultados.
string spike = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
string outDir = Path.Combine(spike, "out");
string dll = Path.Combine(outDir, "VitralTapSpike.dll");
string logs = Path.Combine(outDir, "logs");
string csv = Path.Combine(spike, "resultados", "rotas.csv");
if (!File.Exists(dll)) { Console.WriteLine($"DLL não encontrada: {dll}"); return 1; }
Directory.CreateDirectory(logs);
Directory.CreateDirectory(Path.GetDirectoryName(csv)!);

// Só um consumidor de XAML Diagnostics por processo.
foreach (var nome in new[] { "TranslucentTB", "windhawk", "ExplorerBlurMica" })
{
    if (Process.GetProcessesByName(nome).Length > 0)
    {
        Console.WriteLine($"Feche {nome} antes (só um consumidor de XAML Diagnostics por processo).");
        return 3;
    }
}

// Resolve o alvo: PID e uma janela (para a rota interna).
(uint pid, IntPtr janela, uint thread) = alvo == "explorer" ? Nativo.AlvoExplorer() : Nativo.AlvoIniciar();
if (pid == 0) { Console.WriteLine($"Alvo '{alvo}' não encontrado (no Iniciar: abra-o uma vez antes)."); return 1; }
Console.WriteLine($"Alvo {alvo}: PID {pid}, thread {thread}");

if (Mutex.TryOpenExisting($"Local\\VitralTap-{pid}", out var existente))
{
    existente.Dispose();
    Console.WriteLine("Já anexado a este processo (mutex existe). Nada feito.");
    Registrar("JA_ANEXADO", 0);
    return 2;
}

string dados = $"hostpid={(semHost ? 0 : Environment.ProcessId)};modo={modo}";
File.WriteAllText(Path.Combine(outDir, "init.txt"), dados);

using var pronto = new EventWaitHandle(false, EventResetMode.ManualReset, $"Local\\VitralTapReady-{pid}");
var relogio = Stopwatch.StartNew();
IntPtr hook = IntPtr.Zero;

if (rota == "externa")
{
    int hr = XamlDiag.Carregar(pid, dll, clsid, dados);
    if (hr != 0) Console.WriteLine($"InitializeXamlDiagnosticsEx falhou: 0x{hr:X8}");
}
else
{
    hook = RotaInterna.Instalar(dll, janela, thread);
}

bool ok = pronto.WaitOne(TimeSpan.FromSeconds(35));
long ms = relogio.ElapsedMilliseconds;
RotaInterna.Remover(hook);

// Processos AppContainer (Iniciar) não enxergam o evento Local\ do injector: confere pelo log.
string log = Path.Combine(logs, $"tap-{pid}.log");
Thread.Sleep(2000);
string conteudo = File.Exists(log) ? File.ReadAllText(log) : "";
if (!ok && conteudo.Contains("ADVISE|hr=0")) ok = true;

int fills = Contar(conteudo, "FILL_APLICADO|");
string frames = conteudo.Split('\n').LastOrDefault(l => l.Contains("FRAMES="))?.Split("FRAMES=")[1].Trim() ?? "0";
string resultado = ok ? $"OK(frames={frames};fills={fills})" : "TIMEOUT";
Console.WriteLine($"Resultado: {resultado} em {ms} ms. Log: {log}");
Registrar(resultado, ms);

if (!semHost && ok)
{
    Console.WriteLine("Olhe as barras. Pressione Enter para encerrar este host (a barra deve voltar ao normal).");
    Console.ReadLine();
}
return ok ? 0 : 4;

void Registrar(string res, long tempo) =>
    File.AppendAllText(csv, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss};{rota};{alvo};{pid};{res};{tempo}{Environment.NewLine}");

static int Contar(string texto, string trecho)
{
    int n = 0, i = 0;
    while ((i = texto.IndexOf(trecho, i, StringComparison.Ordinal)) >= 0) { n++; i += trecho.Length; }
    return n;
}

internal static class Nativo
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? cls, string? title);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);

    public static (uint, IntPtr, uint) AlvoExplorer()
    {
        IntPtr tray = FindWindowW("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return (0, IntPtr.Zero, 0);
        uint thread = GetWindowThreadProcessId(tray, out uint pid);
        return (pid, tray, thread);
    }

    public static (uint, IntPtr, uint) AlvoIniciar()
    {
        var proc = Process.GetProcessesByName("StartMenuExperienceHost").FirstOrDefault();
        if (proc is null) return (0, IntPtr.Zero, 0);
        uint alvoPid = (uint)proc.Id;
        IntPtr achada = IntPtr.Zero; uint achadaThread = 0;
        EnumWindows((h, _) =>
        {
            uint t = GetWindowThreadProcessId(h, out uint p);
            if (p != alvoPid) return true;
            achada = h; achadaThread = t; return false;
        }, IntPtr.Zero);
        return (alvoPid, achada, achadaThread);
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// As janelas do app do Claude: qual é a da frente em cada monitor e que conversa cada uma mostra (spec: pedido do
/// Gustavo, 30/10). A ordem do EnumWindows é a de cima para baixo; a primeira janela "de verdade" de cada monitor é a
/// que está à vista nele. A conversa vem do endereço dos documentos pela acessibilidade (UI Automation).
internal static partial class JanelasDoClaude
{
    private const int GwlExStyle = -20, DwmCloaked = 14;
    private const long Ferramenta = 0x80, Transparente = 0x20, SempreNoTopo = 0x8;
    private static readonly string[] ClassesDoShell = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    private delegate bool Visitar(IntPtr h, IntPtr l);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool EnumWindows(Visitar v, IntPtr l);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWindowVisible(IntPtr h);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsIconic(IntPtr h);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetWindowRect(IntPtr h, out Retangulo r);
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial long GetWindowLongPtr(IntPtr h, int i);
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(IntPtr h, out uint processo);
    [LibraryImport("user32.dll")] private static partial IntPtr MonitorFromWindow(IntPtr h, uint quando);
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetClassName(IntPtr h, [Out] char[] nome, int tamanho);
    [LibraryImport("dwmapi.dll")] private static partial int DwmGetWindowAttribute(IntPtr h, int atributo, out int valor, int tamanho);

    [StructLayout(LayoutKind.Sequential)] private struct Retangulo { public int Esquerda, Topo, Direita, Base; }

    /// As janelas do Claude, cada uma dizendo se é a da frente no seu monitor; só as da frente têm os endereços lidos.
    public static IReadOnlyList<JanelaClaude> Listar()
    {
        var claude = Pids("claude");
        if (claude.Count == 0) return [];
        var vitral = Pids("Vitral");
        var monitoresVistos = new HashSet<IntPtr>();
        var achadas = new List<(IntPtr Janela, bool NaFrente)>();
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var processo);
            if (!Conta(h, vitral.Contains(processo))) return true;
            bool primeiraDoMonitor = monitoresVistos.Add(MonitorFromWindow(h, 2 /* o mais próximo */));
            if (claude.Contains(processo)) achadas.Add((h, primeiraDoMonitor));
            return true;
        }, IntPtr.Zero);
        return achadas.Select(a => new JanelaClaude(a.NaFrente, a.NaFrente ? Enderecos(a.Janela) : [])).ToList();
    }

    private static int _aquecendo;

    /// Do Vitral, a cada minuto: mantém ligada a acessibilidade do Claude (o Chromium só monta a árvore depois que
    /// alguém pergunta). Uma rodada por vez: com o Claude travado, não empilha.
    public static void Aquecer()
    {
        if (Interlocked.Exchange(ref _aquecendo, 1) == 1) return;
        Task.Run(() =>
        {
            try { Listar(); }
            catch (Exception) { /* só aquecimento */ }
            finally { Volatile.Write(ref _aquecendo, 0); }
        });
    }

    private static HashSet<uint> Pids(string nome) =>
        Process.GetProcessesByName(nome).Select(p => { using (p) return (uint)p.Id; }).ToHashSet();

    /// Uma janela que alguém vê: visível, não minimizada nem escondida pelo Windows, com tamanho, fora do shell e das
    /// sobreposições (as transparentes ao mouse e as sempre-no-topo do Vitral, como o notch e a doca). A sempre-no-topo
    /// de outro app (vídeo, Gerenciador de Tarefas) cobre o que está atrás e conta.
    private static bool Conta(IntPtr h, bool doVitral)
    {
        if (!IsWindowVisible(h) || IsIconic(h)) return false;
        if (DwmGetWindowAttribute(h, DwmCloaked, out int escondida, sizeof(int)) == 0 && escondida != 0) return false;
        long estilo = GetWindowLongPtr(h, GwlExStyle);
        if ((estilo & (Ferramenta | Transparente)) != 0 || (doVitral && (estilo & SempreNoTopo) != 0)) return false;
        if (!GetWindowRect(h, out var r) || r.Direita - r.Esquerda < 50 || r.Base - r.Topo < 50) return false;
        var nome = new char[64];
        int n = GetClassName(h, nome, nome.Length);
        return !ClassesDoShell.Contains(new string(nome, 0, n));
    }

    private const int MaxNos = 1500, MaxProfundidade = 14;

    /// Os endereços dos documentos da janela: a casca do app e a página da conversa ("claude.ai/epitaxy/local_…"). Em
    /// largura e com limite: para no documento da conversa, sem percorrer a conversa inteira (milhares de nós).
    private static IReadOnlyList<string> Enderecos(IntPtr h)
    {
        var lista = new List<string>();
        try
        {
            var caminhante = TreeWalker.ControlViewWalker;
            var fila = new Queue<(AutomationElement No, int Profundidade)>();
            fila.Enqueue((AutomationElement.FromHandle(h), 0));
            int vistos = 0;
            while (fila.Count > 0 && vistos++ < MaxNos)
            {
                var (no, profundidade) = fila.Dequeue();
                if (no.Current.ControlType == ControlType.Document)
                {
                    if (no.TryGetCurrentPattern(ValuePattern.Pattern, out var p) && ((ValuePattern)p).Current.Value is { Length: > 0 } v)
                    {
                        lista.Add(v);
                        if (v.StartsWith("https://claude.ai/", StringComparison.OrdinalIgnoreCase)) break; // achou a conversa
                    }
                }
                if (profundidade >= MaxProfundidade) continue;
                for (var filho = caminhante.GetFirstChild(no); filho is not null; filho = caminhante.GetNextSibling(filho))
                    fila.Enqueue((filho, profundidade + 1));
            }
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or COMException or TimeoutException) { }
        return lista;
    }
}

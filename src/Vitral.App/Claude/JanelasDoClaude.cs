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
        var claude = Process.GetProcessesByName("claude").Select(p => { using (p) return (uint)p.Id; }).ToHashSet();
        if (claude.Count == 0) return [];
        var monitoresVistos = new HashSet<IntPtr>();
        var achadas = new List<(IntPtr Janela, bool NaFrente)>();
        EnumWindows((h, _) =>
        {
            if (!Conta(h)) return true;
            bool primeiraDoMonitor = monitoresVistos.Add(MonitorFromWindow(h, 2 /* o mais próximo */));
            GetWindowThreadProcessId(h, out var processo);
            if (claude.Contains(processo)) achadas.Add((h, primeiraDoMonitor));
            return true;
        }, IntPtr.Zero);
        return achadas.Select(a => new JanelaClaude(a.NaFrente, a.NaFrente ? Enderecos(a.Janela) : [])).ToList();
    }

    /// Uma janela que alguém vê: visível, não minimizada nem escondida pelo Windows, com tamanho, fora do shell e das
    /// sobreposições (as sempre-no-topo, como o notch e a doca do Vitral, e as transparentes ao mouse).
    private static bool Conta(IntPtr h)
    {
        if (!IsWindowVisible(h) || IsIconic(h)) return false;
        if (DwmGetWindowAttribute(h, DwmCloaked, out int escondida, sizeof(int)) == 0 && escondida != 0) return false;
        long estilo = GetWindowLongPtr(h, GwlExStyle);
        if ((estilo & (Ferramenta | Transparente | SempreNoTopo)) != 0) return false;
        if (!GetWindowRect(h, out var r) || r.Direita - r.Esquerda < 50 || r.Base - r.Topo < 50) return false;
        var nome = new char[64];
        int n = GetClassName(h, nome, nome.Length);
        return !ClassesDoShell.Contains(new string(nome, 0, n));
    }

    /// Os endereços dos documentos da janela: a casca do app e a página da conversa ("claude.ai/epitaxy/local_…").
    private static IReadOnlyList<string> Enderecos(IntPtr h)
    {
        try
        {
            var documentos = AutomationElement.FromHandle(h).FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            var lista = new List<string>();
            foreach (AutomationElement d in documentos)
                if (d.TryGetCurrentPattern(ValuePattern.Pattern, out var p) && ((ValuePattern)p).Current.Value is { Length: > 0 } v) lista.Add(v);
            return lista;
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or COMException) { return []; }
    }
}

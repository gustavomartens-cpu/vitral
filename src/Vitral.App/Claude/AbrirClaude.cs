using System.Diagnostics;
using System.Runtime.InteropServices;
using Vitral.App.Barra;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// Clique no aviso do notch (spec do notch vivo §4.5): traz a janela do app do Claude para a frente. O Windows deixa,
/// porque a última entrada foi o clique no notch. Sem o app aberto, não faz nada.
internal static partial class AbrirClaude
{
    private const int SwRestore = 9;

    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetForegroundWindow(IntPtr h);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool ShowWindow(IntPtr h, int comando);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsIconic(IntPtr h);

    /// Com o id da conversa no app (<paramref name="appId"/>), abre exatamente ela pelo link do app; sem ele, só traz a
    /// janela para a frente.
    public static void Trazer(string? appId = null)
    {
        if (ConversaClaude.Link(appId) is { } link)
        {
            try
            {
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true })?.Dispose();
                return;
            }
            catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("AbrirClaude.Link", e); }
        }
        try
        {
            var janela = Process.GetProcessesByName("claude").Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);
            if (janela == IntPtr.Zero) return;
            if (IsIconic(janela)) ShowWindow(janela, SwRestore);
            SetForegroundWindow(janela);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("AbrirClaude", e); }
    }
}

using System.Runtime.InteropServices;

namespace Vitral.Spike.Injector;

/// Rota interna (padrão atual do TranslucentTB): um hook WH_CALLWNDPROC na thread de uma janela
/// do alvo faz o Windows carregar a DLL lá dentro; a DLL chama InitializeXamlDiagnosticsEx de dentro.
internal static class RotaInterna
{
    private const int WH_CALLWNDPROC = 4;
    private const uint WM_NULL = 0x0000;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string path);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, IntPtr proc, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    /// Instala o hook, provoca uma mensagem na janela e devolve o handle do hook (ou zero em falha).
    public static IntPtr Instalar(string dll, IntPtr janela, uint threadId)
    {
        IntPtr modulo = LoadLibraryW(dll);
        if (modulo == IntPtr.Zero) { Console.WriteLine($"  interna: LoadLibrary falhou ({Marshal.GetLastWin32Error()})"); return IntPtr.Zero; }
        IntPtr proc = GetProcAddress(modulo, "VitralHookProc");
        if (proc == IntPtr.Zero) { Console.WriteLine("  interna: VitralHookProc não exportado"); return IntPtr.Zero; }
        IntPtr hook = SetWindowsHookExW(WH_CALLWNDPROC, proc, modulo, threadId);
        if (hook == IntPtr.Zero) { Console.WriteLine($"  interna: SetWindowsHookEx falhou ({Marshal.GetLastWin32Error()})"); return IntPtr.Zero; }
        SendMessageTimeoutW(janela, WM_NULL, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 2000, out _);
        Console.WriteLine("  interna: hook instalado e mensagem enviada");
        return hook;
    }

    public static void Remover(IntPtr hook)
    {
        if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
    }
}

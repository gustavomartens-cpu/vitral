using System.Runtime.InteropServices;

namespace Vitral.App.Barra;

/// Retângulo (pixels) e DPI atuais do monitor principal. O app é PerMonitorV2: SystemParameters guarda a escala
/// da inicialização e erra depois de uma troca de escala ou de monitor principal (revisão final, I1).
internal static partial class MonitorPrincipal
{
    private const uint MonitorDefaultToPrimary = 1;

    [StructLayout(LayoutKind.Sequential)] private struct Ponto { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Ret { public int Esq, Topo, Dir, Base; }
    [StructLayout(LayoutKind.Sequential)]
    private struct InfoMonitor { public uint Tamanho; public Ret Monitor, Trabalho; public uint Flags; }

    [LibraryImport("user32.dll")] private static partial IntPtr MonitorFromPoint(Ponto ponto, uint flags);
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr monitor, ref InfoMonitor info);
    [LibraryImport("shcore.dll")] private static partial int GetDpiForMonitor(IntPtr monitor, int tipo, out uint dpiX, out uint dpiY);

    public readonly record struct Leitura(int X, int Y, int Largura, int Altura, uint Dpi);

    public static Leitura Ler()
    {
        var monitor = MonitorFromPoint(new Ponto(), MonitorDefaultToPrimary);
        var info = new InfoMonitor { Tamanho = (uint)Marshal.SizeOf<InfoMonitor>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new InvalidOperationException("GetMonitorInfo falhou.");
        uint dpi = GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out uint x, out _) == 0 ? x : 96;
        var r = info.Monitor;
        return new Leitura(r.Esq, r.Topo, r.Dir - r.Esq, r.Base - r.Topo, dpi);
    }
}

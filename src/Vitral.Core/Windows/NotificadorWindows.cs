using System.Runtime.InteropServices;
using Vitral.Core.Nativo;

namespace Vitral.Core.Windows;

/// Avisa as janelas abertas de que o tema ou a barra mudaram (mesma sequência do Light Switch do PowerToys).
public sealed partial class NotificadorWindows : INotificador
{
    private static readonly IntPtr HwndBroadcast = new(0xFFFF);
    private const uint WmSettingChange = 0x001A;
    private const uint WmThemeChanged = 0x031A;
    private const uint WmDwmColorizationColorChanged = 0x0320;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint TimeoutMs = 5000;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr SendMessageTimeoutTexto(IntPtr hwnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr resultado);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr resultado);

    public void AvisarMudancaDeTema()
    {
        SendMessageTimeoutTexto(HwndBroadcast, WmSettingChange, IntPtr.Zero, "ImmersiveColorSet", SmtoAbortIfHung, TimeoutMs, out _);
        SendMessageTimeout(HwndBroadcast, WmThemeChanged, IntPtr.Zero, IntPtr.Zero, SmtoAbortIfHung, TimeoutMs, out _);
        SendMessageTimeout(HwndBroadcast, WmDwmColorizationColorChanged, IntPtr.Zero, IntPtr.Zero, SmtoAbortIfHung, TimeoutMs, out _);
    }

    public void AvisarMudancaDaBarra() =>
        SendMessageTimeoutTexto(HwndBroadcast, WmSettingChange, IntPtr.Zero, "TraySettings", SmtoAbortIfHung, TimeoutMs, out _);

    private const uint AbmSetState = 10;
    private const int AbsAutoHide = 1, AbsAlwaysOnTop = 2;

    [StructLayout(LayoutKind.Sequential)] private struct Ret { public int Esq, Topo, Dir, Base; }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData { public uint Tamanho; public IntPtr Janela; public uint MensagemRetorno; public uint Borda; public Ret Retangulo; public IntPtr Parametro; }

    [LibraryImport("shell32.dll")] private static partial UIntPtr SHAppBarMessage(uint mensagem, ref AppBarData dados);
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr FindWindow(string classe, string? titulo);

    /// A barra de tarefas muda na hora; o Explorer grava o StuckRects3 ao ligar (ao desligar, quem grava é o Vitral).
    public void AvisarOcultarAutomatico(bool ligado)
    {
        var dados = new AppBarData
        {
            Tamanho = (uint)Marshal.SizeOf<AppBarData>(),
            Janela = FindWindow("Shell_TrayWnd", null),
            Parametro = ligado ? AbsAutoHide : AbsAlwaysOnTop,
        };
        if (dados.Janela != IntPtr.Zero) SHAppBarMessage(AbmSetState, ref dados);
    }
}

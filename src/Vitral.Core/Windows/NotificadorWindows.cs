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
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Vitral.App.Barra;

/// O fundo da barra única (spec das formas §5.2): uma faixa de vidro de lado a lado, atrás das ilhas de mídia e de status
/// (que ficam sem vidro próprio) e do notch. Sem conteúdo e sem clique útil.
internal sealed partial class JanelaFundoBarra : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial IntPtr GetLong(IntPtr h, int i);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static partial IntPtr SetLong(IntPtr h, int i, IntPtr v);

    private readonly Border _casca = new() { BorderThickness = new Thickness(1) };

    public JanelaFundoBarra()
    {
        Title = "Vitral · Barra";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        Content = _casca;
        VidroIlha.Preparar(this, _casca);
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetLong(h, GwlExStyle, (IntPtr)((long)GetLong(h, GwlExStyle) | WsExToolWindow | WsExNoActivate));
        };
    }

    public void AplicarCantosRetos(bool retos)
    {
        if (VidroIlha.CantosRetos(this) == retos) return;
        VidroIlha.DefinirCantosRetos(this, retos);
        VidroIlha.Atualizar(this, _casca);
    }
}

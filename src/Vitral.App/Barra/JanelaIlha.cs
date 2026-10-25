using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Vitral.App.Barra;

/// Uma ilha: janela sem borda, sempre no topo, que nunca rouba o foco nem aparece na barra de tarefas nem no Alt+Tab.
internal sealed partial class JanelaIlha : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial IntPtr GetLong(IntPtr h, int i);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static partial IntPtr SetLong(IntPtr h, int i, IntPtr v);

    private readonly Border _casca;

    /// <param name="presaNaBorda">Notch: a janela sobe <see cref="Ui.Barra.PosicaoIlhas.NotchSobra"/> px além da borda
    /// (os cantos de cima ficam fora da tela) e o conteúdo fica nos 34 px visíveis.</param>
    public JanelaIlha(string nome, bool presaNaBorda = false)
    {
        Title = $"Vitral · {nome}";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        SizeToContent = SizeToContent.Width;
        Height = Ui.Barra.PosicaoIlhas.Altura;
        Foreground = (Brush)Application.Current.Resources["Texto"];
        FontFamily = (FontFamily)Application.Current.Resources["FonteCorpo"];
        FontSize = 12;

        _casca = new Border
        {
            CornerRadius = new CornerRadius(Ui.Barra.PosicaoIlhas.Altura / 2),
            BorderBrush = (Brush)Application.Current.Resources["BordaVidro"],
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 0, 12, 0),
        };
        Content = _casca;
        VidroIlha.Preparar(this, _casca);
        if (presaNaBorda)
        {
            Height = Ui.Barra.PosicaoIlhas.NotchAltura;
            _casca.Padding = new Thickness(18, Ui.Barra.PosicaoIlhas.NotchSobra, 18, 0);
            _casca.CornerRadius = new CornerRadius(0, 0, 9, 9);
            _casca.BorderThickness = new Thickness(1, 0, 1, 1);
        }
        // Transparência do Windows ligada/desligada com a barra aberta: troca entre acrílico e sólido.
        Microsoft.Win32.UserPreferenceChangedEventHandler aoMudar = (_, e) =>
        {
            if (e.Category is Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Color)
                Dispatcher.BeginInvoke(() => VidroIlha.Atualizar(this, _casca));
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += aoMudar;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= aoMudar;
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetLong(h, GwlExStyle, (IntPtr)((long)GetLong(h, GwlExStyle) | WsExToolWindow | WsExNoActivate));
        };
    }

    public object? Conteudo
    {
        get => _casca.Child;
        set => _casca.Child = (UIElement?)value;
    }
}

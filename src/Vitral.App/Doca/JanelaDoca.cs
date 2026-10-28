using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Vitral.App.Barra;

namespace Vitral.App.Doca;

/// A doca: ilha de vidro como as do topo, sempre no topo, sem roubar o foco e fora da barra de tarefas e do Alt+Tab.
internal sealed partial class JanelaDoca : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial IntPtr GetLong(IntPtr h, int i);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static partial IntPtr SetLong(IntPtr h, int i, IntPtr v);

    private readonly Border _casca;

    public JanelaDoca()
    {
        Title = "Vitral · Doca";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        SizeToContent = SizeToContent.Width;
        Height = Tema.ServicoFormas.Instancia.Atual.Doca.Altura;
        Tema.ServicoFormas.Instancia.RegistrarDoca(this); // tamanhos dos ícones e ampliação (spec das formas §5.2)
        Foreground = (Brush)Application.Current.Resources["Texto"];
        FontFamily = (FontFamily)Application.Current.Resources["FonteCorpo"];

        _casca = new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["BordaVidro"],
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 0, 10, 0),
        };
        Content = _casca;
        VidroIlha.Preparar(this, _casca, Tema.ParteAparencia.Doca);
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

    /// Formas da doca (spec das formas §4.1): altura e cantos; na corrida, a largura do monitor (em DIPs), e o
    /// conteúdo centralizado.
    public void AplicarFormas(Core.Formas.ValoresDoca doca, double larguraMonitor)
    {
        if (doca.Formato == Core.Perfis.FormatoDoca.Corrida)
        {
            SizeToContent = SizeToContent.Manual;
            Width = larguraMonitor;
        }
        else if (SizeToContent != SizeToContent.Width)
        {
            ClearValue(WidthProperty);
            SizeToContent = SizeToContent.Width;
        }
        Height = doca.Altura;
        // O vidro só se refaz quando os cantos mudam: Posicionar roda a cada app que abre ou fecha.
        if (VidroIlha.CantosRetos(this) == doca.CantosRetos) return;
        VidroIlha.DefinirCantosRetos(this, doca.CantosRetos);
        VidroIlha.Atualizar(this, _casca);
    }

    public object? Conteudo
    {
        get => _casca.Child;
        set => _casca.Child = (UIElement?)value;
    }
}

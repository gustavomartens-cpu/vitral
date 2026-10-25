using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Painel que desce de uma ilha (spec v2 §3.2): janela de vidro como as ilhas, mas ativável (recebe Esc e digitação).
/// Fecha quando perde o foco ou com Esc; quem decide é o <see cref="CoordenadorPaineis"/>.
internal sealed partial class JanelaPainel : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80;
    private static readonly Duration Entrada = new(TimeSpan.FromMilliseconds(120));

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial IntPtr GetLong(IntPtr h, int i);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static partial IntPtr SetLong(IntPtr h, int i, IntPtr v);

    private readonly Border _casca;
    private readonly TranslateTransform _deslize = new();

    /// A janela perdeu o foco (clique fora): o coordenador decide se fecha.
    public event Action? PerdeuFoco;

    /// Esc dentro do painel.
    public event Action? PediuFechar;

    public JanelaPainel(string nome, double largura)
    {
        Title = $"Vitral · {nome}";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        Width = largura;
        SizeToContent = SizeToContent.Height;
        Foreground = (Brush)Application.Current.Resources["Texto"];
        FontFamily = (FontFamily)Application.Current.Resources["FonteCorpo"];
        FontSize = 12;

        _casca = new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["BordaVidro"],
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 14, 16, 14),
            RenderTransform = _deslize,
        };
        Content = _casca;
        VidroIlha.Preparar(this, _casca);
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
            SetLong(h, GwlExStyle, (IntPtr)((long)GetLong(h, GwlExStyle) | WsExToolWindow)); // fora do Alt+Tab
        };
        Deactivated += (_, _) => PerdeuFoco?.Invoke();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            PediuFechar?.Invoke();
        };
    }

    public object? Conteudo
    {
        get => _casca.Child;
        set => _casca.Child = (UIElement?)value;
    }

    /// Mostra em <paramref name="posicao"/> (DIPs) com o deslize de 8 px em 120 ms.
    public void Mostrar(Retangulo posicao)
    {
        Left = posicao.X;
        Top = posicao.Y;
        Show();
        Activate();
        _deslize.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-8, 0, Entrada) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}

/// Conteúdo de ilha que pede um painel (clique no relógio, na rede, no volume, na bateria, na mídia).
internal interface IPedePainel
{
    event Action<Painel>? Pediu;
}

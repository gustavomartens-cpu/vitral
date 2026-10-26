using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Painel que desce de uma ilha (spec v2 §3.2): janela de vidro como as ilhas, mas ativável (recebe Esc e digitação).
/// Fecha quando perde o foco ou com Esc; quem decide é o <see cref="CoordenadorPaineis"/>.
internal sealed partial class JanelaPainel : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial IntPtr GetLong(IntPtr h, int i);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static partial IntPtr SetLong(IntPtr h, int i, IntPtr v);

    private readonly Border _casca;
    private System.Windows.Threading.DispatcherTimer? _vigiaMenu;
    private DateTime _inicioMenu;
    private bool _vistoMenu;

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr FindWindowEx(IntPtr pai, IntPtr depois, string? classe, string? titulo);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWindowVisible(IntPtr h);
    [LibraryImport("user32.dll")] private static partial IntPtr GetForegroundWindow();
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(IntPtr h, out uint processo);
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)] private static partial int GetClassName(IntPtr h, [Out] char[] nome, int tamanho);

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
        Deactivated += (_, _) => { if (_vigiaMenu is null) PerdeuFoco?.Invoke(); };
        Activated += (_, _) => PararVigia();
        Closed += (_, _) => PararVigia();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            PediuFechar?.Invoke();
        };
    }

    /// Clique direito num ícone da bandeja: o menu do app tira o foco do painel, que fica aberto enquanto o menu existir.
    public void SegurarDuranteMenu()
    {
        PararVigia();
        _inicioMenu = DateTime.Now;
        _vistoMenu = false;
        _vigiaMenu = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _vigiaMenu.Tick += (_, _) => VigiarMenu();
        _vigiaMenu.Start();
    }

    private void VigiarMenu()
    {
        if (IsActive) { PararVigia(); return; }
        bool menu = MenuVisivel();
        _vistoMenu |= menu;
        switch (MenuDoIcone.Decidir(menu, _vistoMenu, DateTime.Now - _inicioMenu, OutraJanelaNaFrente()))
        {
            case DecisaoMenu.Esperar: return;
            case DecisaoMenu.Reativar:
                PararVigia();
                Activate();
                if (!IsActive) PerdeuFoco?.Invoke(); // o Windows não devolveu o foco: fecha como antes
                return;
            default:
                PararVigia();
                PerdeuFoco?.Invoke();
                return;
        }
    }

    private void PararVigia()
    {
        _vigiaMenu?.Stop();
        _vigiaMenu = null;
    }

    /// Menu do Windows ("#32768") visível em algum lugar.
    private static bool MenuVisivel()
    {
        for (var h = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "#32768", null); h != IntPtr.Zero; h = FindWindowEx(IntPtr.Zero, h, "#32768", null))
            if (IsWindowVisible(h)) return true;
        return false;
    }

    /// Uma janela visível de outro app está na frente (o app abriu algo pelo menu). A janela escondida que o app usa
    /// para o menu não conta, nem a área de trabalho.
    private static bool OutraJanelaNaFrente()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero || !IsWindowVisible(h)) return false;
        GetWindowThreadProcessId(h, out var processo);
        if (processo == (uint)Environment.ProcessId) return false;
        var nome = new char[64];
        var classe = new string(nome, 0, Math.Max(0, GetClassName(h, nome, nome.Length)));
        return classe is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "#32768");
    }

    public object? Conteudo
    {
        get => _casca.Child;
        set => _casca.Child = (UIElement?)value;
    }

    /// Mostra em <paramref name="posicao"/> (DIPs), assentando de cima com o conteúdo em cascata.
    public void Mostrar(Retangulo posicao)
    {
        Left = posicao.X;
        Top = posicao.Y;
        Show();
        Activate();
        Tema.Efeitos.Assentar(_casca, Tema.AssentarDe.Cima, cascata: true);
    }
}

/// Conteúdo de ilha que pede um painel (clique no relógio, na rede, no volume, na bateria, na mídia).
internal interface IPedePainel
{
    event Action<Painel>? Pediu;
}

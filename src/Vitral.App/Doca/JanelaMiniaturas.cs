using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Vitral.App.Barra;
using Vitral.Ui.Barra;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

/// Miniaturas das janelas de um app (spec da doca §5): ilha de vidro acima do botão, uma miniatura do DWM por janela
/// (200 px de largura, na proporção da janela), com o título e um "×". Sem foco, fora da barra e do Alt+Tab.
internal sealed partial class JanelaMiniaturas : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
    private const double Largura = 200, AlturaMinima = 60, AlturaMaxima = 160;
    private const int DwmDestino = 0x1, DwmOpacidade = 0x4, DwmVisivel = 0x8, DwmSoCliente = 0x10;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial IntPtr GetLong(IntPtr h, int i);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static partial IntPtr SetLong(IntPtr h, int i, IntPtr v);
    [LibraryImport("dwmapi.dll")] private static partial int DwmRegisterThumbnail(IntPtr destino, IntPtr origem, out IntPtr miniatura);
    [LibraryImport("dwmapi.dll")] private static partial int DwmUnregisterThumbnail(IntPtr miniatura);
    [LibraryImport("dwmapi.dll")] private static partial int DwmUpdateThumbnailProperties(IntPtr miniatura, in Propriedades p);
    [LibraryImport("dwmapi.dll")] private static partial int DwmQueryThumbnailSourceSize(IntPtr miniatura, out Tamanho t);

    [StructLayout(LayoutKind.Sequential)] private struct Ret { public int Esq, Topo, Dir, Baixo; }
    [StructLayout(LayoutKind.Sequential)] private struct Tamanho { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Propriedades
    {
        public int Flags;
        public Ret Destino, Origem;
        public byte Opacidade;
        public int Visivel, SoCliente;
    }

    private readonly Border _casca;
    private readonly StackPanel _linha = new() { Orientation = Orientation.Horizontal };
    private readonly List<(FrameworkElement Lugar, IntPtr Miniatura)> _miniaturas = [];
    private IntPtr _handle;

    /// O botão cujas janelas estão na tela (null quando escondida).
    public BotaoAppViewModel? Botao { get; private set; }

    /// As janelas mostradas, na ordem.
    public IReadOnlyList<IntPtr> Handles { get; private set; } = [];

    public JanelaMiniaturas()
    {
        Title = "Vitral · Miniaturas";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Foreground = (Brush)Application.Current.Resources["Texto"];
        FontFamily = (FontFamily)Application.Current.Resources["FonteCorpo"];
        FontSize = 11;
        _casca = new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["BordaVidro"],
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = _linha,
        };
        Content = _casca;
        VidroIlha.Preparar(this, _casca, Tema.ParteAparencia.Doca);
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            SetLong(_handle, GwlExStyle, (IntPtr)((long)GetLong(_handle, GwlExStyle) | WsExToolWindow | WsExNoActivate));
        };
        LayoutUpdated += (_, _) => AtualizarMiniaturas();
        Closed += (_, _) => Liberar();
    }

    /// Mostra as janelas de <paramref name="botao"/> acima do botão (<paramref name="centroBotao"/> em DIPs).
    public void Mostrar(BotaoAppViewModel botao, DocaViewModel vm, Retangulo doca, double centroBotao, Retangulo monitor)
    {
        try
        {
            Liberar();
            Botao = botao;
            Handles = botao.Janelas.Select(j => j.Handle).ToList();
            if (_handle == IntPtr.Zero) _handle = new WindowInteropHelper(this).EnsureHandle();
            foreach (var j in botao.Janelas) _linha.Children.Add(Celula(j, vm));
            bool novo = !IsVisible; // trocar de ícone ou fechar uma janela com as miniaturas abertas não sobe de novo
            Show();
            UpdateLayout();
            var r = PosicaoIlhas.Miniaturas(doca, centroBotao, ActualWidth, ActualHeight, monitor);
            Left = r.X;
            // Sobe assentando (spec do ajuste visual §4.1). É a janela que se move, não o conteúdo: as miniaturas são
            // desenhadas pelo DWM por cima e acompanham a janela, não o RenderTransform do WPF.
            if (novo)
                BeginAnimation(TopProperty, new System.Windows.Media.Animation.DoubleAnimation(r.Y + 8, r.Y, Tema.Efeitos.Lenta)
                    { EasingFunction = Tema.Efeitos.Mola, FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop });
            Top = r.Y;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("Miniaturas.Mostrar", e); Esconder(); }
    }

    public void Esconder()
    {
        Liberar();
        Hide();
    }

    private FrameworkElement Celula(JanelaApp j, DocaViewModel vm)
    {
        double altura = AlturaMaxima * 0.7;
        if (DwmRegisterThumbnail(_handle, j.Handle, out var miniatura) == 0)
        {
            if (DwmQueryThumbnailSourceSize(miniatura, out var t) == 0 && t.X > 0)
                altura = Math.Clamp(Largura * t.Y / t.X, AlturaMinima, AlturaMaxima);
        }
        else miniatura = IntPtr.Zero;

        var lugar = new Border { Width = Largura, Height = altura, CornerRadius = new CornerRadius(4) };
        lugar.SetResourceReference(Border.BackgroundProperty, "Linha");
        if (miniatura != IntPtr.Zero) _miniaturas.Add((lugar, miniatura));

        // O título corta com "…" na largura da miniatura (o inteiro fica na dica): um título longo não alarga a célula
        // nem empurra as outras janelas para fora do card.
        var titulo = new TextBlock { Text = j.Titulo, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = j.Titulo };
        var fechar = new Button
        {
            Content = "",
            Style = (Style)Application.Current.Resources["BotaoIlha"],
            ToolTip = "Fechar janela",
        };
        fechar.Click += (_, _) => vm.FecharJanelaCommand.Execute(j);
        var cabecalho = new DockPanel { Margin = new Thickness(2, 0, 0, 6), LastChildFill = true };
        DockPanel.SetDock(fechar, Dock.Right);
        cabecalho.Children.Add(fechar);
        cabecalho.Children.Add(titulo);

        var conteudo = new StackPanel { Width = Largura };
        conteudo.Children.Add(cabecalho);
        conteudo.Children.Add(lugar);
        var celula = new Border
        {
            Child = conteudo,
            Padding = new Thickness(6),
            Margin = new Thickness(2, 0, 2, 0),
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
        };
        celula.MouseEnter += (_, _) => celula.Background = (Brush)celula.FindResource("SuperficieHover"); // a da doca
        celula.MouseLeave += (_, _) => celula.Background = Brushes.Transparent;
        celula.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject o && EhDentro(o, fechar)) return;
            vm.EscolherJanelaCommand.Execute(j);
            Esconder();
        };
        return celula;
    }

    private static bool EhDentro(DependencyObject o, DependencyObject pai)
    {
        for (var x = o; x is not null; x = VisualTreeHelper.GetParent(x))
            if (ReferenceEquals(x, pai)) return true;
        return false;
    }

    /// O DWM desenha cada miniatura no retângulo do seu lugar, em pixels da janela.
    private void AtualizarMiniaturas()
    {
        if (!IsVisible) return;
        double escala = VisualTreeHelper.GetDpi(this).DpiScaleX;
        foreach (var (lugar, miniatura) in _miniaturas)
        {
            if (!lugar.IsDescendantOf(this)) continue;
            var p = lugar.TransformToAncestor(this).Transform(new Point(0, 0));
            var props = new Propriedades
            {
                Flags = DwmDestino | DwmOpacidade | DwmVisivel | DwmSoCliente,
                Destino = new Ret
                {
                    Esq = (int)Math.Round(p.X * escala),
                    Topo = (int)Math.Round(p.Y * escala),
                    Dir = (int)Math.Round((p.X + lugar.ActualWidth) * escala),
                    Baixo = (int)Math.Round((p.Y + lugar.ActualHeight) * escala),
                },
                Opacidade = 255,
                Visivel = 1,
                SoCliente = 0,
            };
            DwmUpdateThumbnailProperties(miniatura, in props);
        }
    }

    private void Liberar()
    {
        foreach (var (_, miniatura) in _miniaturas) DwmUnregisterThumbnail(miniatura);
        _miniaturas.Clear();
        _linha.Children.Clear();
        Botao = null;
        Handles = [];
    }
}

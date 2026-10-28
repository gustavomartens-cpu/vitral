using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vitral.Ui.Tema;

namespace Vitral.App.Tema;

/// De onde o que aparece chega: do topo (painéis) ou de baixo (o que sobe da doca).
public enum AssentarDe { Cima, Baixo }

/// Chave geral do movimento: a opção "Efeitos de animação" do Windows, lida do sistema a cada animação.
/// O SystemParameters.ClientAreaAnimation do WPF guarda o valor da abertura e não acompanhou a troca (teste de 25/10).
internal static partial class Movimento
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint acao, uint parametro, out int valor, uint flags);

    public static bool Ligado => !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out int ligado, 0) || ligado != 0;
}

/// Peças de movimento do tema (spec do ajuste visual §3.3). Mexem só em RenderTransform e Opacity, nunca no layout,
/// e com a animação do Windows desligada vão direto ao estado final.
public static class Efeitos
{
    // ---- tokens -------------------------------------------------------------------------------------------------

    internal static Duration Tempo(string chave, double padraoMs)
    {
        var normal = Application.Current?.TryFindResource(chave) is Duration { HasTimeSpan: true } d ? d.TimeSpan : TimeSpan.FromMilliseconds(padraoMs);
        return new Duration(TempoMovimento.Duracao(normal, Movimento.Ligado));
    }

    internal static Duration Rapida => Tempo("DuracaoRapida", 120);
    internal static Duration Media => Tempo("DuracaoMedia", 180);
    internal static Duration Lenta => Tempo("DuracaoLenta", 260);
    internal static Duration Clique => Tempo("DuracaoClique", 60);
    internal static Duration TempoBrilho => Tempo("DuracaoBrilho", 550);

    internal static IEasingFunction Mola =>
        Application.Current?.TryFindResource("CurvaMola") as IEasingFunction ?? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

    internal static IEasingFunction Suave =>
        Application.Current?.TryFindResource("CurvaSuave") as IEasingFunction ?? new CubicEase { EasingMode = EasingMode.EaseOut };

    // ---- transformações próprias de cada peça, no mesmo TransformGroup -------------------------------------------

    private static readonly DependencyProperty EscalaAfundarProperty =
        DependencyProperty.RegisterAttached("EscalaAfundar", typeof(ScaleTransform), typeof(Efeitos));

    internal static readonly DependencyProperty EscalaLupaProperty =
        DependencyProperty.RegisterAttached("EscalaLupa", typeof(ScaleTransform), typeof(Efeitos));

    private static readonly DependencyProperty TranslacaoProperty =
        DependencyProperty.RegisterAttached("Translacao", typeof(TranslateTransform), typeof(Efeitos));

    /// O grupo de transformações do elemento, criado na primeira vez (preserva a transformação que já existia).
    private static TransformGroup Grupo(UIElement e)
    {
        if (e.RenderTransform is TransformGroup { IsFrozen: false } g) return g;
        var novo = new TransformGroup();
        if (e.RenderTransform is { } t && !ReferenceEquals(t, Transform.Identity))
            novo.Children.Add(t.IsFrozen ? t.Clone() : t);
        e.RenderTransform = novo;
        return novo;
    }

    internal static ScaleTransform EscalaDe(UIElement e, DependencyProperty chave)
    {
        if (e.GetValue(chave) is ScaleTransform s) return s;
        s = new ScaleTransform();
        Grupo(e).Children.Add(s);
        e.SetValue(chave, s);
        return s;
    }

    private static TranslateTransform TranslacaoDe(UIElement e)
    {
        if (e.GetValue(TranslacaoProperty) is TranslateTransform t) return t;
        // Reaproveita uma translação que já era a transformação do elemento (o deslize antigo do painel).
        t = e.RenderTransform as TranslateTransform is { IsFrozen: false } existente ? existente : new TranslateTransform();
        if (!ReferenceEquals(e.RenderTransform, t)) Grupo(e).Children.Add(t);
        e.SetValue(TranslacaoProperty, t);
        return t;
    }

    internal static void AnimarEscala(ScaleTransform s, double alvo, Duration duracao, IEasingFunction? curva)
    {
        var a = new DoubleAnimation(alvo, duracao) { EasingFunction = curva };
        s.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        s.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    // ---- Afundar: escala 0,9 no clique, volta com mola ao soltar ou sair ----------------------------------------

    public static readonly DependencyProperty AfundarProperty =
        DependencyProperty.RegisterAttached("Afundar", typeof(bool), typeof(Efeitos), new PropertyMetadata(false, AoMudarAfundar));

    public static bool GetAfundar(DependencyObject o) => (bool)o.GetValue(AfundarProperty);
    public static void SetAfundar(DependencyObject o, bool v) => o.SetValue(AfundarProperty, v);

    private static void AoMudarAfundar(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not UIElement el) return;
        if ((bool)e.NewValue)
        {
            if (el.RenderTransformOrigin == default) el.RenderTransformOrigin = new Point(0.5, 0.5);
            el.PreviewMouseLeftButtonDown += Apertou;
            el.PreviewMouseLeftButtonUp += Soltou;
            el.MouseLeave += Soltou;
            el.LostMouseCapture += Soltou;
        }
        else
        {
            el.PreviewMouseLeftButtonDown -= Apertou;
            el.PreviewMouseLeftButtonUp -= Soltou;
            el.MouseLeave -= Soltou;
            el.LostMouseCapture -= Soltou;
        }
    }

    private static void Apertou(object sender, MouseButtonEventArgs e) =>
        AnimarEscala(EscalaDe((UIElement)sender, EscalaAfundarProperty), 0.9, Clique, null);

    private static void Soltou(object sender, EventArgs e)
    {
        if (((UIElement)sender).GetValue(EscalaAfundarProperty) is ScaleTransform s && s.ScaleX != 1)
            AnimarEscala(s, 1, Rapida, Mola);
    }

    // ---- Lupa: na doca, o ícone sob o mouse cresce e os vizinhos conforme a distância ---------------------------

    public static readonly DependencyProperty LupaProperty =
        DependencyProperty.RegisterAttached("Lupa", typeof(bool), typeof(Efeitos), new PropertyMetadata(false, AoMudarLupa));

    public static bool GetLupa(DependencyObject o) => (bool)o.GetValue(LupaProperty);
    public static void SetLupa(DependencyObject o, bool v) => o.SetValue(LupaProperty, v);

    /// Marca o que cresce dentro de um elemento com Lupa.
    public static readonly DependencyProperty AlvoDaLupaProperty =
        DependencyProperty.RegisterAttached("AlvoDaLupa", typeof(bool), typeof(Efeitos), new PropertyMetadata(false, AoMudarAlvoDaLupa));

    public static bool GetAlvoDaLupa(DependencyObject o) => (bool)o.GetValue(AlvoDaLupaProperty);
    public static void SetAlvoDaLupa(DependencyObject o, bool v) => o.SetValue(AlvoDaLupaProperty, v);

    private static void AoMudarLupa(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not UIElement el) return;
        el.MouseMove -= LupaMoveu;
        el.MouseLeave -= LupaSaiu;
        if (!(bool)e.NewValue)
        {
            // Desligada com o mouse em cima (formas da doca): o que estava ampliado volta ao tamanho normal.
            foreach (var alvo in AlvosDaLupa(el))
                if (alvo.GetValue(EscalaLupaProperty) is ScaleTransform s)
                {
                    s.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    s.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    s.ScaleX = s.ScaleY = 1;
                }
            return;
        }
        el.MouseMove += LupaMoveu;
        el.MouseLeave += LupaSaiu;
    }

    // Cresce a partir de 3/4 da altura: quase tudo para cima, sem passar da altura da doca (1,22 × 30 px).
    private static void AoMudarAlvoDaLupa(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is UIElement el && (bool)e.NewValue) el.RenderTransformOrigin = new Point(0.5, 0.75);
    }

    private static IEnumerable<FrameworkElement> AlvosDaLupa(DependencyObject raiz)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is FrameworkElement f && GetAlvoDaLupa(f)) { yield return f; continue; }
            foreach (var neto in AlvosDaLupa(filho)) yield return neto;
        }
    }

    /// Aplicada direto, sem animação, para acompanhar o cursor sem atraso.
    private static void LupaMoveu(object sender, MouseEventArgs e)
    {
        if (!Movimento.Ligado || sender is not FrameworkElement raiz) return;
        double x = e.GetPosition(raiz).X;
        foreach (var alvo in AlvosDaLupa(raiz))
        {
            if (!alvo.IsVisible) continue;
            double centro = alvo.TransformToAncestor(raiz).Transform(new Point(alvo.ActualWidth / 2, 0)).X;
            var s = EscalaDe(alvo, EscalaLupaProperty);
            s.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            s.ScaleX = s.ScaleY = Lupa.Escala(x - centro);
        }
    }

    private static void LupaSaiu(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement raiz) return;
        foreach (var alvo in AlvosDaLupa(raiz))
            if (alvo.GetValue(EscalaLupaProperty) is ScaleTransform s) AnimarEscala(s, 1, Rapida, Suave);
    }

    // ---- Assentar: o que aparece chega 8 px e com fade; nos painéis, o conteúdo em cascata ----------------------

    /// Chamado por quem mostra a janela ou o popup. Sempre recomeça do estado inicial.
    public static void Assentar(FrameworkElement alvo, AssentarDe de, bool cascata)
    {
        double inicio = de == AssentarDe.Cima ? -8 : 8;
        TranslacaoDe(alvo).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(inicio, 0, Lenta) { EasingFunction = Mola });
        alvo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Media) { EasingFunction = Suave });
        if (!cascata || PainelRaiz(alvo) is not { } painel) return;
        int i = 0;
        foreach (UIElement filho in painel.Children)
        {
            var atraso = TempoMovimento.Duracao(Cascata.Atraso(i++), Movimento.Ligado);
            TranslacaoDe(filho).BeginAnimation(TranslateTransform.YProperty, Atrasada(inicio / 2, 0, atraso, Media.TimeSpan, Mola));
            filho.BeginAnimation(UIElement.OpacityProperty, Atrasada(0, 1, atraso, Media.TimeSpan, Suave));
        }
    }

    /// Fica no valor inicial durante o atraso e depois anima: sem piscar o valor final antes da hora.
    private static DoubleAnimationUsingKeyFrames Atrasada(double de, double para, TimeSpan atraso, TimeSpan duracao, IEasingFunction curva)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new DiscreteDoubleKeyFrame(de, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        a.KeyFrames.Add(new DiscreteDoubleKeyFrame(de, KeyTime.FromTimeSpan(atraso)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(para, KeyTime.FromTimeSpan(atraso + duracao), curva));
        return a;
    }

    /// O primeiro painel dentro do conteúdo (Border → UserControl → StackPanel), até quatro níveis.
    private static Panel? PainelRaiz(DependencyObject alvo)
    {
        object? atual = alvo;
        for (int n = 0; n < 4 && atual is not null; n++)
        {
            switch (atual)
            {
                case Panel p when !ReferenceEquals(p, alvo): return p;
                case Border b: atual = b.Child; break;
                case ContentControl c: atual = c.Content; break;
                case Decorator d: atual = d.Child; break;
                default: return atual as Panel;
            }
        }
        return null;
    }

    // ---- Deslize: um indicador único desliza até o item escolhido (abas e pílulas) -----------------------------

    /// No Selector cujo template tem um "PainelItens" (o painel dos itens) e um "Indicador" num Canvas por cima dele.
    public static readonly DependencyProperty DeslizeProperty =
        DependencyProperty.RegisterAttached("Deslize", typeof(bool), typeof(Efeitos), new PropertyMetadata(false, AoMudarDeslize));

    public static bool GetDeslize(DependencyObject o) => (bool)o.GetValue(DeslizeProperty);
    public static void SetDeslize(DependencyObject o, bool v) => o.SetValue(DeslizeProperty, v);

    /// Quanto tirar da largura do item (a aba tem 20 px de folga à direita que o sublinhado não cobre).
    public static readonly DependencyProperty DeslizeRecuoProperty =
        DependencyProperty.RegisterAttached("DeslizeRecuo", typeof(double), typeof(Efeitos), new PropertyMetadata(0d));

    public static double GetDeslizeRecuo(DependencyObject o) => (double)o.GetValue(DeslizeRecuoProperty);
    public static void SetDeslizeRecuo(DependencyObject o, double v) => o.SetValue(DeslizeRecuoProperty, v);

    private static void AoMudarDeslize(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not Selector s || !(bool)e.NewValue) return;
        // Depois do layout: a escolhida fica em negrito e muda de largura (e o seletor também) logo após o SelectionChanged.
        s.SelectionChanged += (_, a) =>
        {
            if (ReferenceEquals(a.OriginalSource, s))
                s.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => Deslizar(s, animar: true));
        };
        s.Loaded += (_, _) => Deslizar(s, animar: false);
        // Com o indicador já na tela, o novo tamanho é seguido deslizando (não cancela o deslize em curso).
        s.SizeChanged += (_, _) => Deslizar(s, animar: true);
        s.IsVisibleChanged += (_, _) => { if (s.IsVisible) Deslizar(s, animar: false); };
    }

    private static void Deslizar(Selector s, bool animar)
    {
        s.ApplyTemplate();
        if (s.Template?.FindName("Indicador", s) is not FrameworkElement indicador
            || s.Template.FindName("PainelItens", s) is not FrameworkElement painel) return;
        var item = s.SelectedIndex < 0 ? null : s.ItemContainerGenerator.ContainerFromIndex(s.SelectedIndex) as FrameworkElement;
        if (item is null || item.ActualWidth <= 0 || !item.IsDescendantOf(painel))
        {
            indicador.Visibility = Visibility.Hidden;
            // O item ainda não foi medido (aba nova, janela restaurada): tenta de novo depois do layout.
            if (item is not null) s.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => Deslizar(s, animar: false));
            return;
        }
        var p = item.TransformToAncestor(painel).Transform(new Point(0, 0));
        var (x, largura) = Deslize.Alvo(new Vitral.Ui.Barra.Retangulo(p.X, p.Y, item.ActualWidth, item.ActualHeight),
            new Vitral.Ui.Barra.Retangulo(0, 0, painel.ActualWidth, painel.ActualHeight));
        largura = Math.Max(0, largura - GetDeslizeRecuo(s));
        // Indicador sem altura fixa (a pílula) acompanha a altura do item; o sublinhado tem altura própria.
        if (double.IsNaN(indicador.Height) || indicador.Tag as string == "AlturaDoItem")
        {
            indicador.Tag = "AlturaDoItem";
            indicador.Height = item.ActualHeight;
            Canvas.SetTop(indicador, p.Y);
        }
        bool primeira = indicador.Visibility != Visibility.Visible || indicador.ActualWidth <= 0;
        indicador.Visibility = Visibility.Visible;
        if (!animar || primeira)
        {
            indicador.BeginAnimation(Canvas.LeftProperty, null);
            indicador.BeginAnimation(FrameworkElement.WidthProperty, null);
            Canvas.SetLeft(indicador, x);
            indicador.Width = largura;
            return;
        }
        indicador.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(x, Media) { EasingFunction = Mola });
        indicador.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(largura, Media) { EasingFunction = Mola });
    }

    // ---- Brilho: uma faixa clara atravessa o botão principal a cada entrada do mouse ----------------------------

    /// No botão cujo template tem uma "Faixa" (Border com o gradiente e um TranslateTransform relativo).
    public static readonly DependencyProperty BrilhoProperty =
        DependencyProperty.RegisterAttached("Brilho", typeof(bool), typeof(Efeitos), new PropertyMetadata(false, AoMudarBrilho));

    private static void AoMudarBrilho(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not Control c) return;
        c.MouseEnter -= Brilhar;
        if ((bool)e.NewValue) c.MouseEnter += Brilhar;
    }

    private static void Brilhar(object sender, MouseEventArgs e)
    {
        if (!Movimento.Ligado || sender is not Control c || c.Template?.FindName("Faixa", c) is not Border faixa
            || faixa.Background is not GradientBrush pincel) return;
        if (pincel.IsFrozen) faixa.Background = pincel = pincel.Clone();
        if (pincel.RelativeTransform is not TranslateTransform { IsFrozen: false } t)
            pincel.RelativeTransform = t = new TranslateTransform(-1, 0);
        t.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-1, 1.2, TempoBrilho) { EasingFunction = Suave });
    }

    public static bool GetBrilho(DependencyObject o) => (bool)o.GetValue(BrilhoProperty);
    public static void SetBrilho(DependencyObject o, bool v) => o.SetValue(BrilhoProperty, v);

    public static readonly DependencyProperty PerigoProperty =
        DependencyProperty.RegisterAttached("Perigo", typeof(bool), typeof(Efeitos), new PropertyMetadata(false));

    public static bool GetPerigo(DependencyObject o) => (bool)o.GetValue(PerigoProperty);
    public static void SetPerigo(DependencyObject o, bool v) => o.SetValue(PerigoProperty, v);
}

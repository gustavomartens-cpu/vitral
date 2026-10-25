using System.Windows;
using System.Windows.Controls;
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

    // ---- Marcas usadas pelos estilos (botão principal e itens de perigo nos menus) ------------------------------

    public static readonly DependencyProperty BrilhoProperty =
        DependencyProperty.RegisterAttached("Brilho", typeof(bool), typeof(Efeitos), new PropertyMetadata(false));

    public static bool GetBrilho(DependencyObject o) => (bool)o.GetValue(BrilhoProperty);
    public static void SetBrilho(DependencyObject o, bool v) => o.SetValue(BrilhoProperty, v);

    public static readonly DependencyProperty PerigoProperty =
        DependencyProperty.RegisterAttached("Perigo", typeof(bool), typeof(Efeitos), new PropertyMetadata(false));

    public static bool GetPerigo(DependencyObject o) => (bool)o.GetValue(PerigoProperty);
    public static void SetPerigo(DependencyObject o, bool v) => o.SetValue(PerigoProperty, v);
}

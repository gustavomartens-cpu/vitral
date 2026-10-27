using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.App.Tema;

public enum ParteAparencia { Doca, Notch, Ilhas }

/// Desenha a doca, o notch e as ilhas com a aparência do perfil (spec da aparência §5.1). Cada janela dessas partes
/// junta aos seus recursos o dicionário da parte, que redefine as chaves do tema do Vitral (Texto, Ambar, Linha...) com
/// os valores da aparência; as telas leem essas chaves por DynamicResource, e a janela principal fica com as de
/// sempre. No tema Automático, acompanha o modo e o destaque do Windows.
internal sealed class ServicoAparencia : IControleAparencia
{
    public static ServicoAparencia Instancia { get; } = new();

    private readonly Dictionary<ParteAparencia, ResourceDictionary> _dicionarios = new()
    {
        [ParteAparencia.Doca] = [], [ParteAparencia.Notch] = [], [ParteAparencia.Ilhas] = [],
    };
    private AparenciaPerfil _perfil = EstadoAparencia.Padrao;

    private ServicoAparencia()
    {
        Recalcular();
        // Modo, destaque e transparência do Windows: recalcula (o Automático muda; a transparência vale para todos).
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
                Application.Current?.Dispatcher.BeginInvoke(Recalcular);
        };
    }

    public ValoresAparencia Atual { get; private set; } = null!;

    /// Os valores mudaram: o vidro e o que é desenhado em código se refazem.
    public event Action? Mudou;

    public void Aplicar(AparenciaPerfil aparencia)
    {
        _perfil = aparencia;
        Recalcular();
    }

    public ValoresParte Valores(ParteAparencia parte) => parte switch
    {
        ParteAparencia.Doca => Atual.Doca,
        ParteAparencia.Notch => Atual.Notch,
        _ => Atual.Ilhas,
    };

    /// O elemento (janela, menu) passa a ler as cores da parte.
    public void Registrar(FrameworkElement elemento, ParteAparencia parte)
    {
        var d = _dicionarios[parte];
        if (!elemento.Resources.MergedDictionaries.Contains(d)) elemento.Resources.MergedDictionaries.Add(d);
    }

    private void Recalcular()
    {
        Atual = ResolverAparencia.Resolver(_perfil, LerWindows());
        foreach (var (parte, d) in _dicionarios) Preencher(d, Valores(parte));
        Mudou?.Invoke();
    }

    /// As chaves do tema do Vitral que as telas dessas partes usam (Tokens.xaml), com os valores da aparência.
    private static void Preencher(ResourceDictionary d, ValoresParte v)
    {
        d["Texto"] = Pincel(v.Texto);
        d["TextoSecundario"] = Pincel(v.TextoSecundario);
        d["CorTextoSecundario"] = Cor(v.TextoSecundario);
        d["Ambar"] = Pincel(v.Destaque);
        d["CorAmbar"] = Cor(v.Destaque);
        d["AmbarGradiente"] = Gradiente(v.Destaque);
        d["TextoSobreAmbar"] = Pincel(Luminancia(v.Destaque) > 0.4 ? new CorArgb(255, 0x1B, 0x14, 0x06) : new CorArgb(255, 255, 255, 255));
        d["Linha"] = Pincel(v.Borda);
        d["Borda"] = Pincel(v.Borda);
        d["BordaVidro"] = Pincel(v.Borda);
        d["Superficie"] = Pincel(v.Texto.ComAlfa(15));
        d["SuperficieHover"] = Pincel(v.Texto.ComAlfa(26));
        d["Selecao"] = Pincel(v.Destaque.ComAlfa(64));
        d["NoiteVidro"] = Pincel(v.Fundo);
        d["Noite"] = Pincel(v.Fundo.ComAlfa(255));
        d["FundoMenu"] = Pincel(v.Fundo.ComAlfa(0xDB));
        d["HoverMenu"] = Pincel(v.Destaque.ComAlfa(0x29));
        d["SeparadorMenu"] = Pincel(v.Texto.ComAlfa(0x17));
    }

    /// Modo, destaque e transparência do Windows agora.
    public static VisualWindows LerWindows()
    {
        using var personalizar = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        bool escuro = personalizar?.GetValue("SystemUsesLightTheme") is not int claro || claro == 0;
        bool transparencia = personalizar?.GetValue("EnableTransparency") is not int t || t != 0;
        using var dwm = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        // AccentColor é ABGR.
        string destaque = dwm?.GetValue("AccentColor") is int abgr
            ? $"#{abgr & 0xFF:X2}{(abgr >> 8) & 0xFF:X2}{(abgr >> 16) & 0xFF:X2}" : "#0078D4";
        return new VisualWindows(escuro, destaque, transparencia);
    }

    public static Color Cor(CorArgb c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    public static SolidColorBrush Pincel(CorArgb c)
    {
        var p = new SolidColorBrush(Cor(c));
        p.Freeze();
        return p;
    }

    /// O botão principal das ilhas: um pouco mais claro em cima, mais escuro embaixo (como o âmbar de sempre).
    private static LinearGradientBrush Gradiente(CorArgb c)
    {
        static byte Mais(byte v, double f) => (byte)Math.Clamp(v + (255 - v) * f, 0, 255);
        static byte Menos(byte v, double f) => (byte)Math.Clamp(v * (1 - f), 0, 255);
        var g = new LinearGradientBrush(Color.FromRgb(Mais(c.R, .2), Mais(c.G, .2), Mais(c.B, .2)),
            Color.FromRgb(Menos(c.R, .08), Menos(c.G, .08), Menos(c.B, .08)), new Point(0, 0), new Point(1, 1));
        g.Freeze();
        return g;
    }

    public static double Luminancia(CorArgb c)
    {
        static double Canal(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Canal(c.R) + 0.7152 * Canal(c.G) + 0.0722 * Canal(c.B);
    }
}

using System.Globalization;
using Vitral.Core.Perfis;

namespace Vitral.Core.Aparencia;

/// Uma cor com alfa, sem depender do WPF.
public readonly record struct Cor(byte A, byte R, byte G, byte B)
{
    /// "#RRGGBB" com o alfa dado; null se inválida.
    public static Cor? DeHex(string? hex, byte alfa = 255)
    {
        if (hex is not { Length: 7 } || hex[0] != '#'
            || !uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return null;
        return new Cor(alfa, (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public Cor ComAlfa(byte alfa) => this with { A = alfa };
}

/// O que o Windows diz agora (lido pelo App): para o tema Automático e para a transparência.
public sealed record VisualWindows(bool ModoEscuro, string CorDestaque, bool Transparencia);

/// Os valores finais de uma parte. <paramref name="Desfoque"/>: vidro com desfoque do Windows (nunca no notch).
public sealed record ValoresParte(MaterialParte Material, Cor Fundo, Cor Texto, Cor TextoSecundario, Cor Borda, Cor Destaque,
    CantosParte Cantos, bool Desfoque)
{
    public double RaioPx => Cantos switch { CantosParte.Retos => 0, CantosParte.Suaves => 4, _ => 8 };
}

public sealed record ValoresAparencia(ValoresParte Doca, ValoresParte Notch, ValoresParte Ilhas);

/// A regra da aparência (spec da aparência §4): tema → personalizar geral → diferenciar da parte → derivados (texto,
/// borda, transparência do Windows, notch sem desfoque).
public static class ResolverAparencia
{
    private sealed record Base(MaterialParte Material, string Fundo, int Opacidade, string? Destaque);

    private static readonly Cor TextoEscuro = Cor.DeHex("#1B1D22")!.Value, TextoClaro = Cor.DeHex("#ECEEF2")!.Value;

    public static ValoresAparencia Resolver(AparenciaPerfil? aparencia, VisualWindows windows)
    {
        var a = aparencia ?? new AparenciaPerfil(TemaAparencia.VidroEscuro);
        var (partes, notch) = Tema(a.Tema, windows);
        ValoresParte Parte(Base b, AjustesAparencia? propria, bool ehNotch) => Montar(b, a.Geral, propria, ehNotch, windows);
        return new ValoresAparencia(Parte(partes, a.Doca, false), Parte(notch, a.Notch, true), Parte(partes, a.Ilhas, false));
    }

    /// Valores-base do tema (spec §4.1): para doca e ilhas, e para o notch. Destaque nulo = o do Windows.
    private static (Base Partes, Base Notch) Tema(TemaAparencia tema, VisualWindows w) => tema switch
    {
        TemaAparencia.Automatico when w.ModoEscuro =>
            (new(MaterialParte.Vidro, "#141826", 55, null), new(MaterialParte.Solido, "#0B0C10", 100, null)),
        TemaAparencia.Automatico =>
            (new(MaterialParte.Vidro, "#FAFAFC", 72, null), new(MaterialParte.Solido, "#F4F4F6", 100, null)),
        TemaAparencia.PretoSolido =>
            (new(MaterialParte.Solido, "#0B0C10", 100, "#F2F2F2"), new(MaterialParte.Solido, "#0B0C10", 100, "#F2F2F2")),
        TemaAparencia.Claro =>
            (new(MaterialParte.Vidro, "#FAFAFC", 72, "#2F6FED"), new(MaterialParte.Solido, "#F4F4F6", 100, "#2F6FED")),
        TemaAparencia.VidroColorido =>
            (new(MaterialParte.Vidro, "#783CB4", 42, "#FFD1FF"), new(MaterialParte.Vidro, "#461E6E", 85, "#FFD1FF")),
        TemaAparencia.Grafite =>
            (new(MaterialParte.Solido, "#24262B", 100, "#3DDBB5"), new(MaterialParte.Solido, "#1A1B1F", 100, "#3DDBB5")),
        TemaAparencia.Cristal =>
            (new(MaterialParte.Vidro, "#FFFFFF", 8, "#FFFFFF"), new(MaterialParte.Vidro, "#000000", 55, "#FFFFFF")),
        _ => (new(MaterialParte.Vidro, "#0A0F20", 72, "#F2B544"), new(MaterialParte.Solido, "#0B0C10", 100, "#F2B544")),
    };

    private static ValoresParte Montar(Base b, AjustesAparencia? geral, AjustesAparencia? propria, bool ehNotch, VisualWindows w)
    {
        var material = propria?.Material ?? geral?.Material ?? b.Material;
        var fundo = Cor.DeHex(propria?.CorFundo) ?? Cor.DeHex(geral?.CorFundo) ?? Cor.DeHex(b.Fundo)!.Value;
        int opacidade = (propria?.Transparencia ?? geral?.Transparencia) is { } t ? 100 - Math.Clamp(t, 0, 100) : b.Opacidade;
        var destaque = Cor.DeHex(propria?.CorDestaque) ?? Cor.DeHex(geral?.CorDestaque)
            ?? Cor.DeHex(b.Destaque ?? w.CorDestaque) ?? Cor.DeHex("#F2B544")!.Value;
        var cantos = propria?.Cantos ?? geral?.Cantos ?? CantosParte.Redondos;

        if (material == MaterialParte.Solido) opacidade = 100;
        if (!w.Transparencia) { material = MaterialParte.Solido; opacidade = 100; }
        fundo = fundo.ComAlfa(Alfa(opacidade));

        bool escuro = Luminancia(fundo) > 0.5 && opacidade >= 50; // fundo claro e visível: texto escuro
        var texto = escuro ? TextoEscuro : TextoClaro;
        var borda = escuro ? new Cor(Alfa(8), 0, 0, 0) : new Cor(Alfa(10), 255, 255, 255);
        return new ValoresParte(material, fundo, texto, texto.ComAlfa(Alfa(62)), borda, destaque, cantos,
            Desfoque: material == MaterialParte.Vidro && !ehNotch);
    }

    private static byte Alfa(int porcento) => (byte)Math.Round(porcento / 100.0 * 255, MidpointRounding.AwayFromZero);

    /// Luminância relativa (WCAG), sem o alfa.
    private static double Luminancia(Cor c)
    {
        static double Canal(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Canal(c.R) + 0.7152 * Canal(c.G) + 0.0722 * Canal(c.B);
    }
}

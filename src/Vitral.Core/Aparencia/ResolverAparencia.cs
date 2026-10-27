using System.Globalization;
using Vitral.Core.Perfis;

namespace Vitral.Core.Aparencia;

/// Uma cor com alfa, sem depender do WPF.
public readonly record struct CorArgb(byte A, byte R, byte G, byte B)
{
    /// "#RRGGBB" com o alfa dado; null se inválida.
    public static CorArgb? DeHex(string? hex, byte alfa = 255)
    {
        if (hex is not { Length: 7 } || hex[0] != '#'
            || !uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return null;
        return new CorArgb(alfa, (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public CorArgb ComAlfa(byte alfa) => this with { A = alfa };
}

/// O que o Windows diz agora (lido pelo App): para o tema Automático e para a transparência.
public sealed record VisualWindows(bool ModoEscuro, string CorDestaque, bool Transparencia);

/// Os valores finais de uma parte. <paramref name="Desfoque"/>: vidro com desfoque do Windows (nunca no notch).
public sealed record ValoresParte(MaterialParte Material, CorArgb Fundo, CorArgb Texto, CorArgb TextoSecundario, CorArgb Borda, CorArgb Destaque,
    CantosParte Cantos, bool Desfoque)
{
    public double RaioPx => Cantos switch { CantosParte.Retos => 0, CantosParte.Suaves => 4, _ => 8 };

    private static readonly CorArgb Escuro = new(255, 0x1B, 0x1D, 0x22), Claro = new(255, 0xEC, 0xEE, 0xF2);

    public bool TextoClaro => Luminancia(Texto) > 0.5;

    /// Texto sobre o próprio texto (a pílula principal inverte as cores). Não vem do fundo: no Cristal o fundo é branco
    /// quase transparente e o texto é claro.
    public CorArgb TextoInverso => TextoClaro ? Escuro : Claro;

    /// Fundo do menu de contexto (quase opaco, sem desfoque): o fundo da parte se ele contrasta com o texto; senão um
    /// neutro do lado certo.
    public CorArgb FundoMenu => Contraste(Texto, Fundo.ComAlfa(255)) >= 4.5 ? Fundo.ComAlfa(0xDB)
        : (TextoClaro ? new CorArgb(0xDB, 0x1E, 0x20, 0x26) : new CorArgb(0xDB, 0xFA, 0xFA, 0xFC));

    public static double Luminancia(CorArgb c)
    {
        static double Canal(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Canal(c.R) + 0.7152 * Canal(c.G) + 0.0722 * Canal(c.B);
    }

    private static double Contraste(CorArgb a, CorArgb b)
    {
        double x = Luminancia(a), y = Luminancia(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}

public sealed record ValoresAparencia(ValoresParte Doca, ValoresParte Notch, ValoresParte Ilhas);

/// A regra da aparência (spec da aparência §4): tema → personalizar geral → diferenciar da parte → derivados (texto,
/// borda, transparência do Windows, notch sem desfoque).
public static class ResolverAparencia
{
    private sealed record Base(MaterialParte Material, string Fundo, int Opacidade, string? Destaque);

    private static readonly CorArgb TextoEscuro = CorArgb.DeHex("#1B1D22")!.Value, TextoClaro = CorArgb.DeHex("#ECEEF2")!.Value;

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
        var fundo = CorArgb.DeHex(propria?.CorFundo) ?? CorArgb.DeHex(geral?.CorFundo) ?? CorArgb.DeHex(b.Fundo)!.Value;
        int opacidade = (propria?.Transparencia ?? geral?.Transparencia) is { } t ? 100 - Math.Clamp(t, 0, 100) : b.Opacidade;
        var destaque = CorArgb.DeHex(propria?.CorDestaque) ?? CorArgb.DeHex(geral?.CorDestaque)
            ?? CorArgb.DeHex(b.Destaque ?? w.CorDestaque) ?? CorArgb.DeHex("#F2B544")!.Value;
        var cantos = propria?.Cantos ?? geral?.Cantos ?? CantosParte.Redondos;

        if (material == MaterialParte.Solido) opacidade = 100;
        if (!w.Transparencia) { material = MaterialParte.Solido; opacidade = 100; }
        fundo = fundo.ComAlfa(Alfa(opacidade));

        // Texto escuro sobre fundo claro. No vidro com desfoque vale só a cor: o acrílico do Windows sai da cor do
        // tingimento quase sem depender do alfa (conferido na tela em 27/10: branco a 8% sai claro, azul-noite a 20% sai
        // escuro). Sem desfoque (notch, sólido), um fundo muito transparente deixa ver o papel de parede: texto claro.
        bool desfoque = material == MaterialParte.Vidro && !ehNotch;
        bool escuro = Luminancia(fundo) > 0.5 && (desfoque || opacidade >= 50);
        var texto = escuro ? TextoEscuro : TextoClaro;
        var borda = escuro ? new CorArgb(Alfa(8), 0, 0, 0) : new CorArgb(Alfa(10), 255, 255, 255);
        return new ValoresParte(material, fundo, texto, texto.ComAlfa(Alfa(62)), borda, destaque, cantos,
            Desfoque: desfoque);
    }

    private static byte Alfa(int porcento) => (byte)Math.Round(porcento / 100.0 * 255, MidpointRounding.AwayFromZero);

    /// Luminância relativa (WCAG), sem o alfa.
    private static double Luminancia(CorArgb c)
    {
        static double Canal(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Canal(c.R) + 0.7152 * Canal(c.G) + 0.0722 * Canal(c.B);
    }
}

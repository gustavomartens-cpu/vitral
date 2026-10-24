using System.Globalization;

namespace Vitral.Core.Cores;

/// Cor RGB de 8 bits por canal. No perfil é escrita como "#RRGGBB"; no registro do Windows
/// aparece como ABGR (cor de destaque) ou ARGB (ColorizationColor).
public readonly record struct Cor(byte R, byte G, byte B)
{
    public static Cor DeHex(string texto)
    {
        if (texto is not { Length: 7 } || texto[0] != '#' ||
            !uint.TryParse(texto.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            throw new FormatException($"Cor inválida: \"{texto}\". Use o formato #RRGGBB.");
        return new Cor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    public static Cor DeAbgr(uint abgr) => new((byte)abgr, (byte)(abgr >> 8), (byte)(abgr >> 16));

    public string ParaHex() => $"#{R:X2}{G:X2}{B:X2}";

    public uint ParaAbgr() => 0xFF000000u | ((uint)B << 16) | ((uint)G << 8) | R;

    public uint ParaArgb(byte alfa) => ((uint)alfa << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    /// Interpola em direção a <paramref name="outra"/>: t = 0 devolve esta cor, t = 1 devolve a outra.
    public Cor Misturar(Cor outra, double t)
    {
        static byte Canal(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t, MidpointRounding.AwayFromZero);
        return new Cor(Canal(R, outra.R, t), Canal(G, outra.G, t), Canal(B, outra.B, t));
    }
}

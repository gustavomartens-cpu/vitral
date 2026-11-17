namespace Vitral.Ui.Barra;

/// A cor que tinge as barras do som (spec do notch fechado §3.2): a média da capa, clareada quando ficaria escura demais
/// no notch preto.
public static class CorDaCapa
{
    /// Luminância relativa mínima das barras sobre o preto.
    public const double LuminanciaMinima = 0.45;

    /// Média dos pixels BGRA, sem os quase transparentes (alfa abaixo de 128). Sem nenhum: cinza médio.
    public static (byte R, byte G, byte B) Media(ReadOnlySpan<byte> bgra)
    {
        long r = 0, g = 0, b = 0, n = 0;
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] < 128) continue;
            b += bgra[i]; g += bgra[i + 1]; r += bgra[i + 2]; n++;
        }
        return n == 0 ? ((byte)128, (byte)128, (byte)128) : ((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    /// Mistura com o branco até a luminância mínima (o matiz continua); cor já clara volta igual.
    public static (byte R, byte G, byte B) Clarear((byte R, byte G, byte B) cor)
    {
        if (Luminancia(cor) >= LuminanciaMinima) return cor;
        double baixo = 0, alto = 1;
        for (int i = 0; i < 20; i++)
        {
            double meio = (baixo + alto) / 2;
            if (Luminancia(Misturar(cor, meio)) >= LuminanciaMinima) alto = meio; else baixo = meio;
        }
        return Misturar(cor, alto);
    }

    /// Luminância relativa (sRGB linearizado), de 0 (preto) a 1 (branco).
    public static double Luminancia((byte R, byte G, byte B) cor)
    {
        static double Linear(byte c)
        {
            double v = c / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(cor.R) + 0.7152 * Linear(cor.G) + 0.0722 * Linear(cor.B);
    }

    private static (byte R, byte G, byte B) Misturar((byte R, byte G, byte B) c, double t) =>
        ((byte)Math.Round(c.R + (255 - c.R) * t), (byte)Math.Round(c.G + (255 - c.G) * t), (byte)Math.Round(c.B + (255 - c.B) * t));
}

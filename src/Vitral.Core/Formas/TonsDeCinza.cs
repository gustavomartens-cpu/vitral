namespace Vitral.Core.Formas;

/// Os ícones da doca em preto e branco (spec de 17/11/2026; o Gustavo escolheu o alto contraste na prévia): a
/// luminância de cada pixel (0,299·R + 0,587·G + 0,114·B) passa por uma curva em S que puxa os escuros para o preto e
/// os claros para o branco, com o alfa intacto. Os pixels são BGRA pré-multiplicados (o formato do WPF): a curva vale
/// sobre a luminância sem o alfa e o resultado volta multiplicado por ele, então nunca passa do alfa (sem halo).
public static class TonsDeCinza
{
    /// Inclinação da curva em S (12: meio-tom fica no meio, vermelho puro quase preto, verde puro quase branco).
    private const double Inclinacao = 12;

    private static readonly byte[] Curva = CriarCurva();

    private static byte[] CriarCurva()
    {
        static double S(double l) => 1 / (1 + Math.Exp(-Inclinacao * (l - 0.5)));
        double zero = S(0), um = S(1);
        var curva = new byte[256];
        for (int i = 0; i < 256; i++) curva[i] = (byte)Math.Round((S(i / 255.0) - zero) / (um - zero) * 255);
        return curva;
    }

    public static void Converter(Span<byte> bgra)
    {
        if (bgra.Length % 4 != 0) throw new ArgumentException("O tamanho precisa ser múltiplo de 4 (BGRA).", nameof(bgra));
        for (int i = 0; i < bgra.Length; i += 4)
        {
            int alfa = bgra[i + 3];
            byte c = 0;
            if (alfa > 0)
            {
                int luz = (114 * bgra[i] + 587 * bgra[i + 1] + 299 * bgra[i + 2] + 500) / 1000;
                int semAlfa = Math.Min(255, (luz * 255 + alfa / 2) / alfa);
                c = (byte)((Curva[semAlfa] * alfa + 127) / 255);
            }
            bgra[i] = bgra[i + 1] = bgra[i + 2] = c;
        }
    }
}

/// As duas camadas do ícone em preto e branco (revisão final, I1): a colorida fica escondida enquanto a cinza está
/// inteira por cima (senão a cor vaza nas bordas semitransparentes) e aparece inteira assim que a cinza começa a sumir,
/// para o ícone não ficar transparente no meio do fade.
public static class CamadasIcone
{
    public static double OpacidadeOriginal(bool temCinza, double opacidadeCinza) => temCinza && opacidadeCinza >= 1 ? 0 : 1;
}

namespace Vitral.Core.Formas;

/// Os ícones da doca em preto e branco (spec de 17/11/2026): cada pixel BGRA pré-multiplicado (o formato do WPF) vira
/// o cinza da sua luminância (0,299·R + 0,587·G + 0,114·B), com o alfa intacto. Como a mistura é linear, a conta vale
/// direto sobre os valores pré-multiplicados e nunca passa do alfa (sem halo nas bordas).
public static class TonsDeCinza
{
    public static void Converter(Span<byte> bgra)
    {
        if (bgra.Length % 4 != 0) throw new ArgumentException("O tamanho precisa ser múltiplo de 4 (BGRA).", nameof(bgra));
        for (int i = 0; i < bgra.Length; i += 4)
        {
            int cinza = (114 * bgra[i] + 587 * bgra[i + 1] + 299 * bgra[i + 2] + 500) / 1000;
            byte c = (byte)Math.Min(cinza, bgra[i + 3]);
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

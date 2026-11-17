namespace Vitral.Ui.Barra;

/// As barras com a música tocando noutro aparelho (Spotify no Echo Dot, por exemplo): o Windows diz que toca, mas nenhum
/// som sai do PC para medir. Depois de <see cref="SegundosSemSom"/> de silêncio, as barras dançam num ritmo próprio, cada
/// uma no seu passo; o som de volta manda de novo (Gustavo, 16/11/2026). Uma pausa curta entre faixas não vira dança.
public sealed class SomOuDanca
{
    public const double SegundosSemSom = 1.5, LimiteSom = 0.02, Minimo = 0.15;

    private static readonly double[] Passos = [5.1, 7.3, 6.2, 8.7, 4.4]; // rad/s
    private DateTime? _ultimoSom, _inicio;

    public IReadOnlyList<double> Proximos(IReadOnlyList<double> bandas, DateTime agora)
    {
        _inicio ??= agora;
        _ultimoSom ??= agora;
        if (bandas.Any(b => b > LimiteSom)) { _ultimoSom = agora; return bandas; }
        if ((agora - _ultimoSom.Value).TotalSeconds < SegundosSemSom) return bandas;
        double t = (agora - _inicio.Value).TotalSeconds;
        return [.. Passos.Select((w, i) => Minimo + (1 - Minimo) * (0.5 + 0.5 * Math.Sin(t * w + i * 1.3)))];
    }
}

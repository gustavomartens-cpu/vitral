namespace Vitral.Ui.Barra;

/// As 5 barras do som que sai (spec do notch fechado §3.2, R3): a força de cada faixa por Goertzel, que custa pouco por
/// olhar só algumas frequências em vez do espectro inteiro. Cada janela (~20 ms) vira 5 valores de 0 a 1: sobem na hora
/// e descem devagar, e um ganho automático lento faz a música baixa ainda mexer as barras.
public sealed class BandasSom
{
    /// Centros das faixas, de grave a agudo (Hz).
    public static readonly double[] Centros = [60, 250, 1000, 4000, 10000];

    /// Cada faixa olha o centro e dois vizinhos e fica com o mais forte (uma frequência só seria estreita demais).
    private static readonly double[] Vizinhos = [0.8, 1, 1.25];

    /// Descida das barras por janela; meia-vida do ganho automático, em janelas de 20 ms (~5 s).
    private const double Descida = 0.85, MeiaVidaGanho = 250;

    /// Abaixo disto (pico da janela) é silêncio; o ganho não passa de 1/ReferenciaMinima (o chiado não vira barra).
    private const double Silencio = 0.001, ReferenciaMinima = 0.005;

    private static readonly double QuedaReferencia = Math.Pow(0.5, 1 / MeiaVidaGanho);

    private readonly double[] _valores = new double[Centros.Length];
    private double _referencia;

    /// Amplitude da frequência na janela: perto de 1 para um seno de amplitude 1 exatamente nela.
    public static double Goertzel(ReadOnlySpan<float> amostras, double frequencia, int taxa)
    {
        if (amostras.IsEmpty || taxa <= 0 || frequencia >= taxa / 2.0) return 0;
        double coef = 2 * Math.Cos(2 * Math.PI * frequencia / taxa), s1 = 0, s2 = 0;
        foreach (var x in amostras)
        {
            double s0 = x + coef * s1 - s2;
            s2 = s1;
            s1 = s0;
        }
        double potencia = s1 * s1 + s2 * s2 - coef * s1 * s2;
        return Math.Sqrt(Math.Max(0, potencia)) * 2 / amostras.Length;
    }

    /// Uma janela de som mono; devolve as 5 barras (0 a 1).
    public IReadOnlyList<double> Processar(ReadOnlySpan<float> mono, int taxa)
    {
        double pico = 0;
        foreach (var x in mono) pico = Math.Max(pico, Math.Abs(x));
        Span<double> brutos = stackalloc double[Centros.Length];
        if (pico >= Silencio)
            for (int i = 0; i < Centros.Length; i++)
            {
                double maior = 0;
                foreach (var v in Vizinhos) maior = Math.Max(maior, Goertzel(mono, Centros[i] * v, taxa));
                brutos[i] = maior;
            }
        double maiorBruto = 0;
        foreach (var b in brutos) maiorBruto = Math.Max(maiorBruto, b);
        _referencia = Math.Max(maiorBruto, _referencia * QuedaReferencia);
        double referencia = Math.Max(_referencia, ReferenciaMinima);
        for (int i = 0; i < _valores.Length; i++)
        {
            double alvo = Math.Min(1, brutos[i] / referencia);
            _valores[i] = alvo >= _valores[i] ? alvo : _valores[i] * Descida;
        }
        return [.. _valores];
    }

    /// Esquece o que tocou (a captura parou).
    public void Zerar()
    {
        Array.Clear(_valores);
        _referencia = 0;
    }
}

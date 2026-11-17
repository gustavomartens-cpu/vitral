using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

/// As barras do som e a cor da capa no notch fechado (spec do notch fechado §3.2, R3).
public sealed class BandasSomTests
{
    private const int Taxa = 48000, Janela = 960; // 20 ms

    private static float[] Seno(double frequencia, double amplitude = 1, int n = Janela, int inicio = 0) =>
        Enumerable.Range(inicio, n).Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * frequencia * i / Taxa))).ToArray();

    [Fact]
    public void Goertzel_acha_o_seno()
    {
        var x = Seno(1000);
        Assert.True(BandasSom.Goertzel(x, 1000, Taxa) > 0.9);
        Assert.True(BandasSom.Goertzel(x, 60, Taxa) < 0.1);
    }

    [Fact]
    public void Seno_de_1kHz_acende_a_banda_do_meio()
    {
        var b = new BandasSom();
        IReadOnlyList<double> v = [];
        for (int i = 0; i < 10; i++) v = b.Processar(Seno(1000, inicio: i * Janela), Taxa);
        Assert.Equal(5, v.Count);
        Assert.Equal(2, v.Select((x, i) => (x, i)).MaxBy(p => p.x).i);
        Assert.True(v[2] > 0.5);
    }

    [Fact]
    public void Silencio_zera()
    {
        var b = new BandasSom();
        var v = b.Processar(new float[Janela], Taxa);
        Assert.All(v, x => Assert.True(x < 0.01));
    }

    [Fact]
    public void Desce_devagar()
    {
        var b = new BandasSom();
        for (int i = 0; i < 10; i++) b.Processar(Seno(1000, inicio: i * Janela), Taxa);
        var v = b.Processar(new float[Janela], Taxa);
        Assert.True(v[2] > 0.3);
        for (int i = 0; i < 30; i++) v = b.Processar(new float[Janela], Taxa);
        Assert.True(v[2] < 0.05);
    }

    [Fact]
    public void Som_baixo_ainda_aparece()
    {
        var b = new BandasSom();
        IReadOnlyList<double> v = [];
        for (int i = 0; i < 100; i++) v = b.Processar(Seno(1000, 0.02, inicio: i * Janela), Taxa);
        Assert.True(v[2] > 0.3);
    }

    [Fact]
    public void Zerar_apaga_tudo()
    {
        var b = new BandasSom();
        for (int i = 0; i < 10; i++) b.Processar(Seno(1000, inicio: i * Janela), Taxa);
        b.Zerar();
        Assert.All(b.Processar(new float[Janela], Taxa), x => Assert.True(x < 0.01));
    }

    [Fact]
    public void Media_de_dois_pixels()
    {
        byte[] bgra = [0, 0, 255, 255, 255, 0, 0, 255]; // vermelho e azul
        var (r, g, b) = CorDaCapa.Media(bgra);
        Assert.InRange(r, 126, 128);
        Assert.Equal(0, g);
        Assert.InRange(b, 126, 128);
    }

    [Fact]
    public void Media_ignora_transparente()
    {
        byte[] bgra = [0, 0, 255, 255, 255, 255, 255, 0];
        Assert.Equal(((byte)255, (byte)0, (byte)0), CorDaCapa.Media(bgra));
    }

    [Fact]
    public void Clarear_cor_escura()
    {
        var c = CorDaCapa.Clarear((20, 10, 40));
        Assert.True(CorDaCapa.Luminancia(c) >= 0.45);
        Assert.True(c.B > c.R && c.B > c.G); // mesmo matiz
    }

    [Fact]
    public void Clarear_mantem_cor_clara() =>
        Assert.Equal(((byte)230, (byte)180, (byte)120), CorDaCapa.Clarear((230, 180, 120)));
}

/// Sem som saindo do PC (música tocando noutro aparelho, como o Echo Dot), as barras dançam num ritmo próprio
/// (Gustavo, 16/11/2026).
public sealed class SomOuDancaTests
{
    private DateTime _agora = new(2026, 11, 16, 22, 0, 0);
    private static readonly double[] Silencio = [0, 0, 0, 0, 0];
    private static readonly double[] Som = [0.2, 0.9, 0.5, 0.3, 0.1];

    [Fact]
    public void Com_som_segue_o_som()
    {
        var s = new SomOuDanca();
        Assert.Equal(Som, s.Proximos(Som, _agora));
    }

    [Fact]
    public void Silencio_curto_segue_o_som() // uma pausa entre faixas não vira dança
    {
        var s = new SomOuDanca();
        s.Proximos(Som, _agora);
        _agora = _agora.AddSeconds(1.4);
        Assert.Equal(Silencio, s.Proximos(Silencio, _agora));
    }

    [Fact]
    public void Silencio_longo_danca()
    {
        var s = new SomOuDanca();
        s.Proximos(Silencio, _agora);
        _agora = _agora.AddSeconds(1.6);
        var a = s.Proximos(Silencio, _agora);
        _agora = _agora.AddSeconds(0.2);
        var b = s.Proximos(Silencio, _agora);
        Assert.All(a.Concat(b), v => Assert.InRange(v, 0.15, 1));
        Assert.NotEqual(a, b); // mexe com o tempo
        Assert.True(a.Distinct().Count() > 1); // cada barra no seu passo
    }

    [Fact]
    public void Som_de_volta_para_a_danca()
    {
        var s = new SomOuDanca();
        s.Proximos(Silencio, _agora);
        _agora = _agora.AddSeconds(3);
        s.Proximos(Silencio, _agora);
        Assert.Equal(Som, s.Proximos(Som, _agora.AddMilliseconds(30)));
    }
}

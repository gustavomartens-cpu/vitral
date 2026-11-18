using Vitral.Core.Formas;

namespace Vitral.Core.Tests;

/// A conta dos ícones da doca em preto e branco (spec de 17/11/2026): BGRA pré-multiplicado, alfa intacto.
public sealed class TonsDeCinzaTests
{
    private static byte[] C(params byte[] bgra)
    {
        TonsDeCinza.Converter(bgra);
        return bgra;
    }

    [Fact]
    public void Vermelho_puro_opaco() => Assert.Equal([76, 76, 76, 255], C(0, 0, 255, 255));

    [Fact]
    public void Verde_e_azul_puros()
    {
        Assert.Equal([150, 150, 150, 255], C(0, 255, 0, 255));
        Assert.Equal([29, 29, 29, 255], C(255, 0, 0, 255));
    }

    [Fact]
    public void Branco_e_preto_nao_mudam()
    {
        Assert.Equal([255, 255, 255, 255], C(255, 255, 255, 255));
        Assert.Equal([0, 0, 0, 255], C(0, 0, 0, 255));
    }

    [Fact]
    public void Transparente_continua_transparente() => Assert.Equal([0, 0, 0, 0], C(0, 0, 0, 0));

    [Fact]
    public void Semitransparente_mantem_alfa_e_nao_passa_do_alfa()
    {
        Assert.Equal([38, 38, 38, 128], C(0, 0, 128, 128));
        var p = C(10, 20, 30, 30);
        Assert.Equal(30, p[3]);
        Assert.True(p[0] <= 30 && p[1] <= 30 && p[2] <= 30);
    }

    [Fact]
    public void Varios_pixels() => Assert.Equal([76, 76, 76, 255, 150, 150, 150, 255], C(0, 0, 255, 255, 0, 255, 0, 255));

    [Fact]
    public void Tamanho_invalido_lanca() => Assert.Throws<ArgumentException>(() => TonsDeCinza.Converter(new byte[6]));
}

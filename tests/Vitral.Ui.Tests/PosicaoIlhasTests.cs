using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PosicaoIlhasTests
{
    [Fact]
    public void Monitor_na_origem()
    {
        var (midia, relogio, status) = PosicaoIlhas.Calcular(new Retangulo(0, 0, 1920, 1032), 300, 140, 360);
        Assert.Equal(new Retangulo(12, 8, 300, 30), midia);
        Assert.Equal(new Retangulo(890, 8, 140, 30), relogio);
        Assert.Equal(new Retangulo(1548, 8, 360, 30), status);
    }

    [Fact]
    public void Monitor_com_origem_negativa()
    {
        var (midia, relogio, status) = PosicaoIlhas.Calcular(new Retangulo(-1080, -477, 1080, 1920), 300, 140, 360);
        Assert.Equal(new Retangulo(-1068, -469, 300, 30), midia);
        Assert.Equal(new Retangulo(-610, -469, 140, 30), relogio);
        Assert.Equal(new Retangulo(-372, -469, 360, 30), status);
    }
}

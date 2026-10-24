using Vitral.Core.Cores;

namespace Vitral.Core.Tests;

public class CorTests
{
    [Fact]
    public void DeHex_le_os_tres_canais()
    {
        var c = Cor.DeHex("#21375E");
        Assert.Equal((byte)0x21, c.R);
        Assert.Equal((byte)0x37, c.G);
        Assert.Equal((byte)0x5E, c.B);
    }

    [Fact]
    public void ParaAbgr_poe_alfa_cheio_e_inverte_a_ordem()
    {
        Assert.Equal(0xFF5E3721u, Cor.DeHex("#21375E").ParaAbgr());
    }

    [Fact]
    public void DeAbgr_le_o_valor_real_do_DWM_desta_maquina()
    {
        Assert.Equal("#060A11", Cor.DeAbgr(0xFF110A06).ParaHex());
    }

    [Fact]
    public void ParaArgb_usa_o_alfa_pedido()
    {
        Assert.Equal(0xC4986A31u, Cor.DeHex("#986A31").ParaArgb(0xC4));
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("azul")]
    [InlineData("21375E")]
    [InlineData("#GG375E")]
    public void DeHex_recusa_formatos_invalidos(string texto)
    {
        Assert.Throws<FormatException>(() => Cor.DeHex(texto));
    }

    [Fact]
    public void Misturar_com_branco_em_meio_caminho()
    {
        var c = new Cor(0, 0, 0).Misturar(new Cor(255, 255, 255), 0.5);
        Assert.Equal(new Cor(128, 128, 128), c);
    }
}

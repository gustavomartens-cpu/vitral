using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

/// Correções da revisão final da barra do topo.
public sealed class RevisaoBarraTests
{
    [Fact]
    public void Atualizacao_antiga_que_termina_depois_e_descartada()
    {
        var ultima = new UltimaAtualizacao();
        int lenta = ultima.Comecar();   // ex.: lendo a capa da faixa anterior
        int rapida = ultima.Comecar();  // ex.: a sessão acabou

        Assert.True(ultima.EhAtual(rapida));
        Assert.False(ultima.EhAtual(lenta));
    }

    [Theory]
    [InlineData(0, 0, 3840, 2160, 144u, 0, 0, 2560, 1440)]
    [InlineData(0, 0, 1920, 1080, 96u, 0, 0, 1920, 1080)]
    [InlineData(-1920, 0, 1920, 1080, 120u, -1536, 0, 1536, 864)]
    public void Monitor_em_pixels_vira_dips_pela_escala_do_proprio_monitor(
        int x, int y, int largura, int altura, uint dpi, double dx, double dy, double dl, double da)
    {
        Assert.Equal(new Retangulo(dx, dy, dl, da), PosicaoIlhas.ParaDips(x, y, largura, altura, dpi));
    }
}

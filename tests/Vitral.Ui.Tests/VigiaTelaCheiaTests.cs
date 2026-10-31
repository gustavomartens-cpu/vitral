using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class VigiaTelaCheiaTests
{
    [Fact]
    public void Avisa_so_quando_muda()
    {
        var v = new VigiaTelaCheia();
        Assert.Null(v.Conferir(false));
        Assert.True(v.Conferir(true));
        Assert.Null(v.Conferir(true)); // o jogo segue na frente: nada a fazer
        Assert.False(v.Conferir(false));
    }

    [Fact]
    public void Aviso_de_saida_do_Windows_com_o_jogo_ainda_na_frente_nao_reexibe()
    {
        // O Windows manda "saiu da tela cheia" (um overlay abriu), mas a janela da frente ainda cobre o monitor.
        var v = new VigiaTelaCheia();
        v.Conferir(true);
        Assert.Null(v.Conferir(true));
        Assert.True(v.EmTelaCheia);
    }
}

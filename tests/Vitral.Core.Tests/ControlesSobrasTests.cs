using Vitral.Core.Controles;

namespace Vitral.Core.Tests;

/// As sobras da revisão do painel de controles (0.16.0).
public sealed class ControlesSobrasTests
{
    [Theory]
    [InlineData("8BitDo Ultimate 2C Wireless")]
    [InlineData("Xbox Wireless Controller")]
    public void Classico_sem_tipo_na_classe_usa_o_nome(string nome)
    {
        // A classe "sem categoria" do 8BitDo não diz que é controle; o nome diz.
        Assert.Equal(TipoAparelho.Controle, RegrasControles.TipoPelaClasse(0x1F, 0, le: false, nome));
    }

    [Fact]
    public void Classe_conhecida_vence_o_nome()
    {
        Assert.Equal(TipoAparelho.Audio, RegrasControles.TipoPelaClasse(4, 0, le: false, "Controller Headset"));
    }

    [Fact]
    public void Juntar_separa_pelo_endereco_e_nao_pelo_nome()
    {
        var lista = RegrasControles.Juntar([
            (0xA1UL, new AparelhoBluetooth("a", "JBL Flip 5", TipoAparelho.Audio, false, true)),
            (0xB2UL, new AparelhoBluetooth("b", "JBL Flip 5", TipoAparelho.Audio, false, true)),
            (0xC3UL, new AparelhoBluetooth("c1", "BT5.0 KB", TipoAparelho.Teclado, false, false)),
            (0xC3UL, new AparelhoBluetooth("c2", "BT5.0 KB", TipoAparelho.Teclado, true, false)),
        ]);
        Assert.Equal(["c1", "a", "b"], lista.Select(a => a.Id)); // o primeiro lado dá o id; conectados primeiro, depois pelo nome
        Assert.True(lista[0].Conectado);
    }

    [Fact]
    public void Juntar_funde_classico_e_le_do_mesmo_aparelho()
    {
        // Fone com o link LE ativo e o áudio clássico desligado: continua fone, com áudio, e aparece conectado.
        var lista = RegrasControles.Juntar([
            (0xA1UL, new AparelhoBluetooth("classico", "Galaxy Buds", TipoAparelho.Audio, false, true)),
            (0xA1UL, new AparelhoBluetooth("le", "Galaxy Buds", TipoAparelho.Outro, true, false)),
        ]);
        var a = Assert.Single(lista);
        Assert.Equal(("classico", TipoAparelho.Audio, true, true), (a.Id, a.Tipo, a.Conectado, a.TemAudio));
    }

    [Fact]
    public void Id_da_tela_externa_segue_a_saida_de_video()
    {
        // Com o monitor da DISPLAY1 desligado, o da DISPLAY2 continua com o mesmo id (antes era a posição na lista).
        Assert.Equal(@"ddc:\\.\DISPLAY2:0", RegrasControles.IdTelaExterna(@"\\.\DISPLAY2", 0));
        Assert.NotEqual(RegrasControles.IdTelaExterna(@"\\.\DISPLAY2", 0), RegrasControles.IdTelaExterna(@"\\.\DISPLAY2", 1));
    }
}

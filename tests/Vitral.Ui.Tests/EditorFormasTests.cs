using Vitral.Core.Formas;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class EditorFormasTests
{
    [Fact]
    public void Nao_mexer_da_nulo()
    {
        var e = new EditorFormasViewModel();
        Assert.Null(e.ParaPerfil());
        Assert.Equal(ResolverFormas.Resolver(null, CantosParte.Redondos), e.Previa);
    }

    [Fact]
    public void Carregar_e_ParaPerfil_ida_e_volta()
    {
        var f = new FormasPerfil(new FormasDoca(FormatoDoca.Corrida, TamanhoIcones.Grande, false),
            new FormasNotch(FormatoNotch.Ilha, TamanhoNotch.Compacto), new FormasIlhas(DisposicaoIlhas.BarraUnica, DistanciaIlhas.Encostadas));
        var e = new EditorFormasViewModel();
        e.Carregar(f);
        Assert.Equal(f, e.ParaPerfil());
        e.Carregar(new FormasPerfil(Notch: new FormasNotch(Tamanho: TamanhoNotch.Grande)));
        Assert.Null(e.FormatoDoca);
        Assert.Equal(new FormasPerfil(null, new FormasNotch(null, TamanhoNotch.Grande), null), e.ParaPerfil());
        e.Carregar(null);
        Assert.Null(e.ParaPerfil());
    }

    [Fact]
    public void Previa_muda_com_os_campos()
    {
        var e = new EditorFormasViewModel();
        e.FormatoDoca = FormatoDoca.Corrida;
        Assert.True(e.Previa.Doca.CantosRetos);
        e.DistanciaIlhas = DistanciaIlhas.Encostadas;
        Assert.Equal(0, e.Previa.Ilhas.Topo);
    }

    // "Não mexer" mantém o que está em uso (decisão do Gustavo): a prévia mostra o em uso nos campos não escolhidos.
    [Fact]
    public void Previa_usa_as_formas_em_uso_nos_campos_em_nao_mexer()
    {
        var e = new EditorFormasViewModel { Atuais = () => new FormasPerfil(new FormasDoca(FormatoDoca.Corrida)) };
        e.FormatoNotch = FormatoNotch.Ilha;
        Assert.True(e.Previa.Doca.CantosRetos);
        Assert.True(e.Previa.Notch.CantosDeCima);
        Assert.Equal(new FormasPerfil(Notch: new FormasNotch(FormatoNotch.Ilha)), e.ParaPerfil()); // o perfil guarda só o escolhido
    }

    [Fact]
    public void Opcoes_das_formas_com_nao_mexer_primeiro()
    {
        Assert.All([Opcoes.FormatosDoca, Opcoes.IconesDoca, Opcoes.Ampliacoes, Opcoes.CoresIconesDoca, Opcoes.FormatosNotch, Opcoes.TamanhosNotch,
            Opcoes.DisposicoesIlhas, Opcoes.DistanciasIlhas], l => Assert.Same(Opcoes.Nulo, l[0].Valor));
        Assert.Equal(["Não mexer", "Encaixado", "Sem orelhas", "Ilha"], Opcoes.FormatosNotch.Select(o => o.Rotulo));
        Assert.Equal(["Não mexer", "Separadas", "Barra única", "Sem fundo"], Opcoes.DisposicoesIlhas.Select(o => o.Rotulo));
        Assert.Equal(["Não mexer", "Ligada", "Desligada"], Opcoes.Ampliacoes.Select(o => o.Rotulo));
    }

    [Fact]
    public void Cor_dos_icones_ida_e_volta()
    {
        var e = new EditorFormasViewModel();
        e.Carregar(new FormasPerfil(new FormasDoca(CorIcones: CorIconesDoca.PretoEBranco)));
        Assert.Equal(CorIconesDoca.PretoEBranco, e.CorIconesDoca);
        Assert.Equal(CorIconesDoca.PretoEBranco, e.ParaPerfil()!.Doca!.CorIcones);
    }

    [Fact]
    public void So_cor_dos_icones_gera_secao_da_doca()
    {
        var e = new EditorFormasViewModel { CorIconesDoca = CorIconesDoca.PretoEBranco };
        Assert.Equal(new FormasPerfil(new FormasDoca(CorIcones: CorIconesDoca.PretoEBranco)), e.ParaPerfil());
    }

    [Fact]
    public void Previa_reflete_cor_dos_icones()
    {
        var e = new EditorFormasViewModel { CorIconesDoca = CorIconesDoca.PretoEBranco };
        Assert.Equal(CorIconesDoca.PretoEBranco, e.Previa.Doca.CorIcones);
    }

    [Fact]
    public void Opcoes_da_cor_dos_icones() =>
        Assert.Equal(["Não mexer", "Originais", "Preto e branco"], Opcoes.CoresIconesDoca.Select(o => o.Rotulo));
}

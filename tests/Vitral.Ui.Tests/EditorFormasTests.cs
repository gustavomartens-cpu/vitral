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

    [Fact]
    public void Opcoes_das_formas_com_nao_mexer_primeiro()
    {
        Assert.All([Opcoes.FormatosDoca, Opcoes.IconesDoca, Opcoes.Ampliacoes, Opcoes.FormatosNotch, Opcoes.TamanhosNotch,
            Opcoes.DisposicoesIlhas, Opcoes.DistanciasIlhas], l => Assert.Same(Opcoes.Nulo, l[0].Valor));
        Assert.Equal(["Não mexer", "Encaixado", "Sem orelhas", "Ilha"], Opcoes.FormatosNotch.Select(o => o.Rotulo));
        Assert.Equal(["Não mexer", "Separadas", "Barra única"], Opcoes.DisposicoesIlhas.Select(o => o.Rotulo));
        Assert.Equal(["Não mexer", "Ligada", "Desligada"], Opcoes.Ampliacoes.Select(o => o.Rotulo));
    }
}

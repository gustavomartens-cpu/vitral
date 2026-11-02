using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class SobrasAparenciaTests
{
    [Fact]
    public void Personalizada_do_perfil_fica_na_lista_depois_de_escolher_outra_cor()
    {
        var e = new EditorAjustesAparencia();
        e.Carregar(new AjustesAparencia(CorFundo: "#123456", CorDestaque: "#ABCDEF"));
        e.CorFundo = "#24262B";
        e.CorDestaque = null;
        Assert.Contains(e.CoresFundo, o => Equals(o.Valor, "#123456"));
        Assert.Contains(e.CoresDestaque, o => Equals(o.Valor, "#ABCDEF"));
        e.CorFundo = "#123456";
        Assert.Single(e.CoresFundo, o => Equals(o.Valor, "#123456"));
        e.Carregar(null);
        Assert.Equal(Opcoes.FundosAparencia, e.CoresFundo);
    }

    [Fact]
    public void Previa_recalcula_quando_o_leitor_do_Windows_e_ligado()
    {
        var e = new EditorAparenciaViewModel();
        e.Carregar(new AparenciaPerfil(TemaAparencia.Automatico));
        var escura = e.Previa;
        e.LerWindows = () => new VisualWindows(false, "#0078D4", true);
        Assert.NotEqual(escura, e.Previa);
        Assert.Equal(ResolverAparencia.Resolver(new AparenciaPerfil(TemaAparencia.Automatico), new VisualWindows(false, "#0078D4", true)), e.Previa);
    }
}

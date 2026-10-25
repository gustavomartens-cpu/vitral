using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public class EditorNativoTests
{
    [Fact]
    public void Campo_nao_definido_continua_nao_mexer_na_ida_e_volta()
    {
        var e = new EditorNativoViewModel();
        e.Carregar(new AjustesNativosPerfil(null, Modo.Claro, null, null, null, null, null, null));

        var p = e.ParaPerfil();

        Assert.Equal(Modo.Claro, p.ModoApps);
        Assert.Null(p.ModoSistema);
        Assert.Null(p.Transparencia);
        Assert.Null(p.CorDestaque);
        Assert.Null(p.AlinhamentoBarra);
        Assert.Equal(Escolha.NaoMexer, e.Transparencia);
    }

    [Fact]
    public void Sim_e_nao_viram_verdadeiro_e_falso()
    {
        var e = new EditorNativoViewModel { Transparencia = Escolha.Sim, DestaqueNosTitulos = Escolha.Nao };
        var p = e.ParaPerfil();
        Assert.True(p.Transparencia);
        Assert.False(p.DestaqueNosTitulos);
    }

    [Fact]
    public void Perfil_completo_sobrevive_a_ida_e_volta()
    {
        var original = new AjustesNativosPerfil(Modo.Escuro, Modo.Claro, "#E81123", false, true, false, true, Alinhamento.Esquerda);
        var e = new EditorNativoViewModel();
        e.Carregar(original);
        Assert.Equal(original, e.ParaPerfil());
    }

    [Fact]
    public void Carregar_nulo_deixa_tudo_em_nao_mexer()
    {
        var e = new EditorNativoViewModel();
        e.Carregar(null);
        Assert.Equal(new AjustesNativosPerfil(null, null, null, null, null, null, null, null), e.ParaPerfil());
    }

    [Fact]
    public void Escolher_cor_e_nao_mexer_pela_paleta()
    {
        var e = new EditorNativoViewModel();
        e.EscolherCorCommand.Execute("#E81123");
        Assert.Equal("#E81123", e.CorDestaque);
        e.EscolherCorCommand.Execute(null);
        Assert.Null(e.CorDestaque);
    }

    [Fact]
    public void Tem_as_18_cores_sugeridas_do_esboco()
    {
        var cores = new EditorNativoViewModel().CoresSugeridas;
        Assert.Equal(18, cores.Count);
        Assert.Contains("#21375E", cores);
        Assert.Contains("#E81123", cores);
    }
}

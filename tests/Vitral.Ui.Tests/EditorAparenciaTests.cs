using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class EditorAparenciaTests
{
    private static EditorAparenciaViewModel Criar() => new() { LerWindows = () => new VisualWindows(true, "#0078D4", true) };

    [Fact]
    public void Nao_mexer_da_nulo()
    {
        var e = Criar();
        e.Carregar(null);
        Assert.Null(e.Tema);
        Assert.Null(e.ParaPerfil());
        Assert.Null(e.Previa);
    }

    [Fact]
    public void Tema_e_campos_vao_para_o_perfil()
    {
        var e = Criar();
        e.Tema = TemaAparencia.Grafite;
        e.Geral.CorDestaque = "#FF8A8A";
        Assert.Equal(new AparenciaPerfil(TemaAparencia.Grafite, Geral: new AjustesAparencia(CorDestaque: "#FF8A8A")), e.ParaPerfil());
    }

    [Fact]
    public void Diferenciar_desmarcado_nao_leva_a_parte()
    {
        var e = Criar();
        e.Tema = TemaAparencia.Cristal;
        e.Notch.Material = MaterialParte.Solido;
        Assert.Null(e.ParaPerfil()!.Notch);
        e.Notch.Diferenciar = true;
        Assert.Equal(new AjustesAparencia(Material: MaterialParte.Solido), e.ParaPerfil()!.Notch);
    }

    [Fact]
    public void Carregar_e_ParaPerfil_ida_e_volta()
    {
        var a = new AparenciaPerfil(TemaAparencia.VidroColorido, Geral: new AjustesAparencia(CorFundo: "#1F6F5C", Transparencia: 30),
            Ilhas: new AjustesAparencia(Cantos: CantosParte.Suaves));
        var e = Criar();
        e.Carregar(a);
        Assert.True(e.Ilhas.Diferenciar);
        Assert.False(e.Doca.Diferenciar);
        Assert.Equal(a, e.ParaPerfil());
    }

    [Fact]
    public void Previa_muda_com_o_tema_e_com_os_campos()
    {
        var e = Criar();
        e.Tema = TemaAparencia.Claro;
        Assert.Equal(CorArgb.DeHex("#1B1D22"), e.Previa!.Ilhas.Texto);
        e.Geral.CorFundo = "#0B0C10";
        Assert.Equal(CorArgb.DeHex("#ECEEF2"), e.Previa!.Ilhas.Texto);
    }

    [Fact]
    public void Opcoes_da_aparencia_cobrem_todos_os_valores_com_do_tema_primeiro()
    {
        Assert.Same(Opcoes.Nulo, Opcoes.TemasAparencia[0].Valor);
        Assert.Equal(Enum.GetValues<TemaAparencia>().Cast<object>(), Opcoes.TemasAparencia.Skip(1).Select(o => o.Valor));
        Assert.Equal(["Do tema", "Vidro", "Sólido"], Opcoes.MateriaisAparencia.Select(o => o.Rotulo));
        Assert.Equal(["Do tema", "Retos", "Suaves", "Redondos"], Opcoes.CantosAparencia.Select(o => o.Rotulo));
        Assert.All([Opcoes.MateriaisAparencia, Opcoes.CantosAparencia, Opcoes.FundosAparencia, Opcoes.DestaquesAparencia],
            l => Assert.Same(Opcoes.Nulo, l[0].Valor));
    }
}

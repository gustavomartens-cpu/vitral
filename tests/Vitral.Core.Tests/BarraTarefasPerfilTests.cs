using Vitral.Core.Perfis;

namespace Vitral.Core.Tests;

public sealed class BarraTarefasPerfilTests
{
    private static readonly BarraTarefasPerfil Completa = new(
        PesquisaBarra.Icone, VisaoTarefas: false, MostrarAreaDeTrabalho: true, AgruparBotoes.QuandoCheia,
        OcultarAutomaticamente: false, TodosOsMonitores: true, Selos: true, Piscar: false, IconesVisiveis: ["Spotify.exe"]);

    [Fact]
    public void Esquema3_ida_e_volta()
    {
        var p = new Perfil(3, "Barra", null, null, null, Completa);
        string json = PerfilJson.Escrever(p);
        Assert.Contains("\"esquema\": 3", json);
        Assert.Contains("\"pesquisa\": \"icone\"", json);
        Assert.Contains("\"agrupar\": \"quandoCheia\"", json);
        Assert.Equal(p, PerfilJson.Ler(json));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Esquemas_1_e_2_leem_barra_de_tarefas_nula(int esquema)
    {
        var p = PerfilJson.Ler($$"""{ "esquema": {{esquema}}, "nome": "A" }""");
        Assert.Null(p.BarraTarefas);
    }

    [Fact]
    public void Lista_vazia_e_diferente_de_ausente()
    {
        var vazia = PerfilJson.Ler("""{ "esquema": 3, "nome": "A", "barraTarefas": { "iconesVisiveis": [] } }""");
        Assert.NotNull(vazia.BarraTarefas!.IconesVisiveis);
        Assert.Empty(vazia.BarraTarefas.IconesVisiveis!);

        var ausente = PerfilJson.Ler("""{ "esquema": 3, "nome": "A", "barraTarefas": { "selos": true } }""");
        Assert.Null(ausente.BarraTarefas!.IconesVisiveis);
    }

    [Fact]
    public void Igualdade_compara_a_lista_por_conteudo() =>
        Assert.Equal(new BarraTarefasPerfil(IconesVisiveis: ["a.exe"]), new BarraTarefasPerfil(IconesVisiveis: ["A.EXE"]));

    [Fact]
    public void Listas_diferentes_nao_sao_iguais() =>
        Assert.NotEqual(new BarraTarefasPerfil(IconesVisiveis: ["a.exe"]), new BarraTarefasPerfil(IconesVisiveis: []));
}

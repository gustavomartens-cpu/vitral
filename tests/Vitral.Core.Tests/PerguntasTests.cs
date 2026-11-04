using System.Text.Json.Nodes;
using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

/// As perguntas do Claude no notch (spec das perguntas §2 e §5).
public sealed class PerguntasTests
{
    private const string DuasPerguntas = """
        {"questions":[
          {"question":"Qual fruta você prefere?","header":"Fruta","options":[{"label":"Banana","description":"Amarela"},{"label":"Maçã","description":"Vermelha"}],"multiSelect":false},
          {"question":"Quais cores?","header":"Cores","options":[{"label":"Azul"},{"label":"Verde"},{"label":"Roxo"}],"multiSelect":true}
        ]}
        """;

    private static string Gancho(string ferramenta, string entrada) =>
        $$"""{"hook_event_name":"PermissionRequest","session_id":"s1","cwd":"G:\\dev\\vitral","tool_name":"{{ferramenta}}","tool_input":{{entrada}}}""";

    private static JsonNode Decisao(string? saida) => JsonNode.Parse(saida!)!["hookSpecificOutput"]!["decision"]!;

    [Fact]
    public void Le_as_perguntas_do_gancho()
    {
        var p = PedidoPermissao.DeGancho(Gancho("AskUserQuestion", DuasPerguntas))!;
        Assert.Equal("Claude pergunta", p.Titulo);
        var q = p.Perguntas!;
        Assert.Equal(2, q.Count);
        Assert.Equal(("Qual fruta você prefere?", "Fruta", false), (q[0].Texto, q[0].Cabecalho, q[0].Multipla));
        Assert.Equal([new Opcao("Banana", "Amarela"), new Opcao("Maçã", "Vermelha")], q[0].Opcoes);
        Assert.True(q[1].Multipla);
        Assert.Equal(["Azul", "Verde", "Roxo"], q[1].Opcoes.Select(o => o.Rotulo));
        Assert.Null(q[1].Opcoes[0].Descricao);
    }

    [Fact]
    public void Perguntas_viajam_na_linha()
    {
        var p = PedidoPermissao.DeGancho(Gancho("AskUserQuestion", DuasPerguntas))!;
        var volta = PedidoPermissao.DeLinha(p.ParaLinha())!;
        Assert.Equal(p.Perguntas!.Select(q => (q.Texto, q.Multipla, string.Join("|", q.Opcoes.Select(o => o.Rotulo)))),
            volta.Perguntas!.Select(q => (q.Texto, q.Multipla, string.Join("|", q.Opcoes.Select(o => o.Rotulo)))));
    }

    [Fact]
    public void Nao_cabe()
    {
        Pergunta Com(int opcoes) => new("Q", null, Enumerable.Range(1, opcoes).Select(i => new Opcao($"O{i}", null)).ToList(), false);
        Assert.True(RegrasPerguntas.Cabe([Com(2)]));
        Assert.False(RegrasPerguntas.Cabe([Com(0)]));
        Assert.False(RegrasPerguntas.Cabe([Com(5)]));
        Assert.False(RegrasPerguntas.Cabe(Enumerable.Repeat(Com(2), 5).ToList()));
        Assert.False(RegrasPerguntas.Cabe(null));
    }

    [Fact]
    public void Respostas_usam_o_texto_exato_da_pergunta()
    {
        const string entrada = """{"questions":[{"question":"Qual é a \"melhor\" opção?","header":"X","options":[{"label":"A"},{"label":"B"}],"multiSelect":false}]}""";
        var gancho = Gancho("AskUserQuestion", entrada);
        var d = Decisao(RespostaPermissao.ParaClaude(new RespostaPedido(EscolhaPermissao.Permitir,
            new Dictionary<string, string> { ["Qual é a \"melhor\" opção?"] = "B" }), gancho));
        Assert.Equal("allow", d["behavior"]!.GetValue<string>());
        Assert.Equal("B", d["updatedInput"]!["answers"]!["Qual é a \"melhor\" opção?"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(entrada)!["questions"], d["updatedInput"]!["questions"]));
    }

    [Fact]
    public void Varias_escolhas_juntam_com_virgula()
    {
        var q = new Pergunta("Quais?", null, [new("Azul", null), new("Verde", null), new("Roxo", null)], true);
        Assert.Equal("Azul, Verde", RegrasPerguntas.Juntar(q, ["Verde", "Azul"]));
    }

    [Fact]
    public void Conversa_nao_escreve_nada()
    {
        Assert.Null(RespostaPermissao.ParaClaude(EscolhaPermissao.Conversa, Gancho("AskUserQuestion", DuasPerguntas)));
    }

    [Fact]
    public void Resposta_viaja_na_linha()
    {
        var linha = RespostaPermissao.Linha("a", new RespostaPedido(EscolhaPermissao.Permitir, new Dictionary<string, string> { ["Qual fruta você prefere?"] = "Maçã" }));
        var r = RespostaPermissao.DaLinha(linha, "a")!;
        Assert.Equal(EscolhaPermissao.Permitir, r.Escolha);
        Assert.Equal("Maçã", r.Respostas!["Qual fruta você prefere?"]);
        Assert.Null(RespostaPermissao.DaLinha(linha, "b"));
    }

    [Fact]
    public void Permissao_comum_nao_muda()
    {
        var gancho = Gancho("Bash", """{"command":"npm test"}""");
        Assert.Null(PedidoPermissao.DeGancho(gancho)!.Perguntas);
        var d = Decisao(RespostaPermissao.ParaClaude(EscolhaPermissao.Permitir, gancho));
        Assert.Null(d["updatedInput"]);
        Assert.Equal("allow", d["behavior"]!.GetValue<string>());
    }
}

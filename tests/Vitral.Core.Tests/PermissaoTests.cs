using System.Text.Json.Nodes;
using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

public sealed class PermissaoTests
{
    private static string Gancho(string ferramenta, string entrada, string? sugestoes = null) =>
        $$"""{"hook_event_name":"PermissionRequest","session_id":"s1","cwd":"G:\\dev\\vitral","tool_name":"{{ferramenta}}","tool_input":{{entrada}}{{(sugestoes is null ? "" : $",\"permission_suggestions\":{sugestoes}")}}}""";

    private static JsonNode? Regra(EscolhaPermissao escolha, string gancho) =>
        Decisao(RespostaPermissao.ParaClaude(escolha, gancho))["updatedPermissions"]?[0]?["rules"]?[0];

    [Theory]
    [InlineData("Bash")]
    [InlineData("PowerShell")]
    public void Ferramenta_de_comando_vira_rodar_um_comando(string ferramenta)
    {
        var p = PedidoPermissao.DeGancho(Gancho(ferramenta, """{"command":"Remove-Item x"}"""))!;
        Assert.Equal("Claude quer rodar um comando", p.Titulo);
        Assert.Equal("Remove-Item x", p.Resumo);
        Assert.Equal("Só este comando, nesta sessão", p.AlcanceSempre);
    }

    [Fact]
    public void Comando_mostrado_sem_perder_asterisco_nem_crase()
    {
        var p = PedidoPermissao.DeGancho(Gancho("PowerShell", """{"command":"del *.log; Write-Host `\"a`\"\n# fim"}"""))!;
        Assert.Equal("del *.log; Write-Host `\"a`\" # fim", p.Resumo);
    }

    [Fact]
    public void Comando_inteiro_vai_para_a_dica()
    {
        var comando = new string('x', 500) + "; rm y";
        var p = PedidoPermissao.DeGancho(Gancho("Bash", $$"""{"command":"{{comando}}"}"""))!;
        Assert.Equal(200, p.Resumo.Length);
        Assert.Equal(comando, p.Completo);
    }

    [Fact]
    public void Comando_com_asterisco_nao_tem_sempre()
    {
        var gancho = Gancho("Bash", """{"command":"rm -rf dist/*"}""");
        Assert.Null(PedidoPermissao.DeGancho(gancho)!.AlcanceSempre);
        var d = Decisao(RespostaPermissao.ParaClaude(EscolhaPermissao.SempreNaSessao, gancho));
        Assert.Equal("allow", (string?)d["behavior"]);
        Assert.Null(d["updatedPermissions"]);
    }

    [Fact]
    public void Sempre_ignora_sugestao_mais_larga_que_o_comando()
    {
        const string sugestoes = """[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"git push:*"}],"behavior":"allow","destination":"localSettings"}]""";
        var regra = Regra(EscolhaPermissao.SempreNaSessao, Gancho("Bash", """{"command":"git push origin main"}""", sugestoes))!;
        Assert.Equal("git push origin main", (string?)regra["ruleContent"]);
    }

    [Fact]
    public void WebFetch_sempre_vale_para_o_dominio()
    {
        var gancho = Gancho("WebFetch", """{"url":"https://exemplo.com/x?y=1"}""");
        Assert.Equal("domain:exemplo.com", (string?)Regra(EscolhaPermissao.SempreNaSessao, gancho)!["ruleContent"]);
        Assert.Equal("Qualquer endereço em exemplo.com, nesta sessão", PedidoPermissao.DeGancho(gancho)!.AlcanceSempre);
    }

    [Fact]
    public void Edicao_avisa_que_o_sempre_vale_para_qualquer_arquivo() =>
        Assert.Equal("Qualquer edição de arquivo, nesta sessão",
            PedidoPermissao.DeGancho(Gancho("Edit", """{"file_path":"C:\\a\\Foo.cs"}"""))!.AlcanceSempre);

    [Fact]
    public void Mcp_avisa_que_o_sempre_vale_para_a_ferramenta() =>
        Assert.Equal("A ferramenta sic · lead_get, nesta sessão", PedidoPermissao.DeGancho(Gancho("mcp__sic__lead_get", "{}"))!.AlcanceSempre);

    [Fact]
    public void Pedido_leva_o_id_da_sessao() => Assert.Equal("s1", PedidoPermissao.DeGancho(Gancho("Bash", """{"command":"x"}"""))!.SessaoId);

    private static JsonNode Decisao(string json) => JsonNode.Parse(json)!["hookSpecificOutput"]!["decision"]!;

    [Fact]
    public void Bash_vira_rodar_um_comando()
    {
        var p = PedidoPermissao.DeGancho(Gancho("Bash", """{"command":"dotnet test\n--nologo"}"""))!;
        Assert.Equal("Claude quer rodar um comando", p.Titulo);
        Assert.Equal("dotnet test --nologo", p.Resumo);
        Assert.Equal("vitral", p.Sessao);
        Assert.Equal("Bash", p.Ferramenta);
        Assert.False(string.IsNullOrEmpty(p.Id));
    }

    [Theory]
    [InlineData("Edit", "Claude quer editar um arquivo")]
    [InlineData("MultiEdit", "Claude quer editar um arquivo")]
    [InlineData("Write", "Claude quer criar um arquivo")]
    public void Edit_e_Write_mostram_o_nome_do_arquivo(string ferramenta, string titulo)
    {
        var p = PedidoPermissao.DeGancho(Gancho(ferramenta, """{"file_path":"C:\\a\\b\\Foo.cs"}"""))!;
        Assert.Equal(titulo, p.Titulo);
        Assert.Equal("Foo.cs", p.Resumo);
    }

    [Fact]
    public void WebFetch_mostra_o_endereco()
    {
        var p = PedidoPermissao.DeGancho(Gancho("WebFetch", """{"url":"https://exemplo.com/x"}"""))!;
        Assert.Equal("Claude quer abrir um endereço", p.Titulo);
        Assert.Equal("https://exemplo.com/x", p.Resumo);
    }

    [Fact]
    public void Mcp_mostra_o_servidor_e_a_ferramenta()
    {
        var p = PedidoPermissao.DeGancho(Gancho("mcp__sic__lead_get", "{}"))!;
        Assert.Equal("Claude quer usar uma ferramenta", p.Titulo);
        Assert.Equal("sic · lead_get", p.Resumo);
    }

    [Fact]
    public void Outra_ferramenta_mostra_o_nome() =>
        Assert.Equal("NotebookEdit", PedidoPermissao.DeGancho(Gancho("NotebookEdit", "{}"))!.Resumo);

    [Fact]
    public void Campo_ausente_usa_o_nome_da_ferramenta() =>
        Assert.Equal("Bash", PedidoPermissao.DeGancho(Gancho("Bash", "{}"))!.Resumo);

    [Fact]
    public void Resumo_cortado_em_200()
    {
        var p = PedidoPermissao.DeGancho(Gancho("Bash", $$"""{"command":"{{new string('x', 500)}}"}"""))!;
        Assert.Equal(200, p.Resumo.Length);
        Assert.EndsWith("…", p.Resumo);
    }

    [Theory]
    [InlineData("""{"hook_event_name":"Stop"}""")]
    [InlineData("")]
    [InlineData("{x")]
    public void Evento_errado_ou_json_ruim_e_null(string json) => Assert.Null(PedidoPermissao.DeGancho(json));

    [Fact]
    public void Linha_do_pedido_ida_e_volta()
    {
        var p = new PedidoPermissao("id1", "vitral", "Bash", "Claude quer rodar um comando", "npm test")
            { SessaoId = "s1", Completo = "npm test", AlcanceSempre = "Só este comando, nesta sessão" };
        Assert.Equal(p, PedidoPermissao.DeLinha(p.ParaLinha()));
        Assert.True(PedidoPermissao.EhPedido(p.ParaLinha()));
    }

    [Fact]
    public void EhPedido_distingue_de_aviso()
    {
        var aviso = new AvisoClaude(TipoAviso.Terminou, "vitral", "ok").ParaLinha();
        Assert.False(PedidoPermissao.EhPedido(aviso));
        Assert.Null(PedidoPermissao.DeLinha(aviso));
    }

    [Fact]
    public void Resposta_com_id_certo_e_errado()
    {
        Assert.Contains("\"escolha\":\"Negar\"", RespostaPermissao.Linha("a", EscolhaPermissao.Negar)); // spec §3.4
        Assert.Equal(EscolhaPermissao.Negar, RespostaPermissao.DaLinha(RespostaPermissao.Linha("a", EscolhaPermissao.Negar), "a"));
        Assert.Null(RespostaPermissao.DaLinha(RespostaPermissao.Linha("a", EscolhaPermissao.Negar), "b"));
        Assert.Null(RespostaPermissao.DaLinha("{", "a"));
    }

    [Fact]
    public void Permitir_vira_allow()
    {
        var j = JsonNode.Parse(RespostaPermissao.ParaClaude(EscolhaPermissao.Permitir, Gancho("Bash", """{"command":"npm test"}""")))!;
        Assert.Equal("PermissionRequest", (string?)j["hookSpecificOutput"]!["hookEventName"]);
        Assert.Equal("allow", (string?)j["hookSpecificOutput"]!["decision"]!["behavior"]);
        Assert.Null(j["hookSpecificOutput"]!["decision"]!["updatedPermissions"]);
    }

    [Fact]
    public void Sempre_sai_com_destino_sessao_mesmo_com_sugestao_de_outro_destino()
    {
        const string sugestoes = """[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"npm test:*"}],"behavior":"allow","destination":"localSettings"}]""";
        var d = Decisao(RespostaPermissao.ParaClaude(EscolhaPermissao.SempreNaSessao, Gancho("Bash", """{"command":"npm test"}""", sugestoes)));
        Assert.Equal("allow", (string?)d["behavior"]);
        var regra = d["updatedPermissions"]![0]!;
        Assert.Equal("addRules", (string?)regra["type"]);
        Assert.Equal("session", (string?)regra["destination"]);
        Assert.Equal("allow", (string?)regra["behavior"]);
        Assert.Equal("npm test", (string?)regra["rules"]![0]!["ruleContent"]);
    }

    [Fact]
    public void Sempre_sem_sugestao_no_Bash_usa_o_comando()
    {
        var regra = Decisao(RespostaPermissao.ParaClaude(EscolhaPermissao.SempreNaSessao, Gancho("Bash", """{"command":"npm test"}""")))["updatedPermissions"]![0]!;
        Assert.Equal("session", (string?)regra["destination"]);
        Assert.Equal("Bash", (string?)regra["rules"]![0]!["toolName"]);
        Assert.Equal("npm test", (string?)regra["rules"]![0]!["ruleContent"]);
    }

    [Fact]
    public void Sempre_sem_sugestao_no_PowerShell_usa_o_comando()
    {
        // Sem o comando, a regra liberaria qualquer comando de PowerShell na sessão.
        var regra = Decisao(RespostaPermissao.ParaClaude(EscolhaPermissao.SempreNaSessao, Gancho("PowerShell", """{"command":"Get-Location"}""")))["updatedPermissions"]![0]!;
        Assert.Equal("PowerShell", (string?)regra["rules"]![0]!["toolName"]);
        Assert.Equal("Get-Location", (string?)regra["rules"]![0]!["ruleContent"]);
    }

    [Fact]
    public void Sempre_sem_sugestao_em_outra_ferramenta_usa_a_ferramenta()
    {
        var regra = Decisao(RespostaPermissao.ParaClaude(EscolhaPermissao.SempreNaSessao, Gancho("mcp__sic__lead_get", "{}")))["updatedPermissions"]![0]!;
        var r = regra["rules"]![0]!.AsObject();
        Assert.Equal("mcp__sic__lead_get", (string?)r["toolName"]);
        Assert.False(r.ContainsKey("ruleContent"));
    }

    [Fact]
    public void Negar_vira_deny_com_recado()
    {
        var d = Decisao(RespostaPermissao.ParaClaude(EscolhaPermissao.Negar, Gancho("Bash", """{"command":"rm -rf x"}""")));
        Assert.Equal("deny", (string?)d["behavior"]);
        Assert.Equal("Negado no notch do Vitral.", (string?)d["message"]);
        Assert.False((bool)d["interrupt"]!);
    }
}

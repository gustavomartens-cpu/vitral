using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

public sealed class AvisoClaudeTests
{
    private const string Usuario = """{"type":"user","message":{"role":"user","content":"faz o commit"}}""";
    private const string Assistente = """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"Pronto, fiz o commit."}]}}""";
    private const string SoFerramenta = """{"type":"assistant","message":{"role":"assistant","content":[{"type":"tool_use","id":"x","name":"Bash","input":{}}]}}""";

    [Fact]
    public void Stop_vira_terminou_com_a_ultima_resposta()
    {
        var a = AvisoClaude.DeGancho("""{"hook_event_name":"Stop","cwd":"G:\\dev\\vitral","transcript_path":"t"}""",
            caminho => caminho == "t" ? Usuario + "\n" + Assistente + "\n" : null);
        Assert.Equal(new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto, fiz o commit.") { Completo = "Pronto, fiz o commit." }, a);
    }

    [Fact]
    public void Notification_vira_esperando_com_a_mensagem()
    {
        var a = AvisoClaude.DeGancho("""{"hook_event_name":"Notification","cwd":"C:\\x\\sic","message":"Claude needs your permission to use Bash"}""", _ => null);
        Assert.Equal(new AvisoClaude(TipoAviso.Esperando, "sic", "Claude needs your permission to use Bash"), a);
    }

    [Theory]
    [InlineData("""{"hook_event_name":"PreToolUse"}""")]
    [InlineData("")]
    [InlineData("{x")]
    public void Evento_desconhecido_ou_json_ruim_e_null(string json) => Assert.Null(AvisoClaude.DeGancho(json, _ => null));

    [Fact]
    public void Sem_cwd_a_sessao_e_Claude() =>
        Assert.Equal("Claude", AvisoClaude.DeGancho("""{"hook_event_name":"Notification","message":"oi"}""", _ => null)!.Sessao);

    [Fact]
    public void Transcricao_ilegivel_deixa_texto_vazio() =>
        Assert.Equal("", AvisoClaude.DeGancho("""{"hook_event_name":"Stop","cwd":"C:\\a","transcript_path":"t"}""", _ => null)!.Texto);

    [Fact]
    public void Ultima_resposta_pula_mensagens_so_de_ferramenta() =>
        Assert.Equal("Pronto, fiz o commit.", AvisoClaude.UltimaRespostaDoAssistente(Assistente + "\n" + SoFerramenta + "\n"));

    [Fact]
    public void Ultima_resposta_ignora_linha_cortada() =>
        Assert.Equal("Pronto, fiz o commit.", AvisoClaude.UltimaRespostaDoAssistente("ssistant\",\"message\":{\"content\":[{\"te\n" + Assistente));

    [Fact]
    public void Encurtar_junta_linhas_tira_markdown_e_corta()
    {
        Assert.Equal("Feito codigo ok", AvisoClaude.Encurtar("**Feito**\n\n`codigo` ok"));
        Assert.Equal("Título", AvisoClaude.Encurtar("## Título"));
        var longo = AvisoClaude.Encurtar(new string('a', 100));
        Assert.Equal(80, longo.Length);
        Assert.EndsWith("…", longo);
    }

    [Fact]
    public void Linha_do_canal_ida_e_volta()
    {
        var a = new AvisoClaude(TipoAviso.Esperando, "vitral", "Posso instalar?") { SessaoId = "s1" };
        Assert.Equal(a, AvisoClaude.DeLinha(a.ParaLinha()));
        Assert.DoesNotContain('\n', a.ParaLinha());
    }

    [Fact]
    public void Aviso_leva_o_id_da_sessao() =>
        Assert.Equal("s9", AvisoClaude.DeGancho("""{"hook_event_name":"Notification","session_id":"s9","cwd":"C:\\x\\sic","message":"oi"}""", _ => null)!.SessaoId);

    [Fact]
    public void Linha_de_64k_passa_e_maior_nao()
    {
        var cabe = new AvisoClaude(TipoAviso.Terminou, "x", "ok") { Completo = new string('a', 60_000), ForaDoApp = true };
        Assert.Equal(cabe, AvisoClaude.DeLinha(cabe.ParaLinha()));
        Assert.Null(AvisoClaude.DeLinha(new AvisoClaude(TipoAviso.Terminou, "x", new string('a', 70_000)).ParaLinha()));
    }

    [Fact]
    public void Completar_tira_markdown_e_mantem_paragrafos() =>
        Assert.Equal("Pronto\n\nUse isto:\na\nb", AvisoClaude.Completar("# Pronto\n\n\n\nUse **isto**:\n- a\n- b"));

    [Fact]
    public void Completar_corta_em_32k()
    {
        var c = AvisoClaude.Completar(new string('x', 40_000));
        Assert.Equal(32_768, c.Length);
        Assert.EndsWith("…", c);
    }

    [Fact]
    public void Stop_traz_o_completo_e_o_fora_do_app()
    {
        const string trecho = """{"type":"assistant","message":{"content":[{"type":"text","text":"# Feito\n\nTudo **certo** aqui, com uma resposta bem mais longa que oitenta caracteres para ver o corte do resumo."}]}}""";
        var a = AvisoClaude.DeGancho("""{"hook_event_name":"Stop","cwd":"G:\\dev\\vitral","transcript_path":"t"}""", _ => trecho, foraDoApp: true)!;
        Assert.Equal("Feito\n\nTudo certo aqui, com uma resposta bem mais longa que oitenta caracteres para ver o corte do resumo.", a.Completo);
        Assert.True(a.ForaDoApp);
        Assert.True(a.Texto.Length <= 80);
    }
}

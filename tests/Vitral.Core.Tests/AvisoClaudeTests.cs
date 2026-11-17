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
        Assert.Equal(new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto, fiz o commit.") { Completo = "Pronto, fiz o commit.", Transcricao = "t" }, a);
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
    public void Completar_mantem_os_numeros_da_lista() =>
        Assert.Equal("Qual?\n1. Refatorar X\n2. Só corrigir Y", AvisoClaude.Completar("Qual?\n1. **Refatorar** X\n2. Só corrigir Y"));

    [Fact]
    public void Linha_com_acentos_e_simbolos_cabe_inteira()
    {
        var a = new AvisoClaude(TipoAviso.Terminou, "x", "ok") { Completo = string.Concat(Enumerable.Repeat("ção─│→", 5000)) };
        Assert.Equal(a, AvisoClaude.DeLinha(a.ParaLinha()));
        var p = PedidoResposta.DeAviso(a, DateTime.Now)!;
        Assert.Equal(p, PedidoResposta.DeLinha(p.ParaLinha()));
        Assert.Equal("ção─│→", RespostaTexto.DaLinha(RespostaTexto.Linha("a", "ção─│→"), "a"));
    }

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

    [Fact]
    public void UserPromptSubmit_vira_Comecou_sem_ler_a_transcricao()
    {
        var a = AvisoClaude.DeGancho("""{"hook_event_name":"UserPromptSubmit","cwd":"C:/p/vitral","session_id":"s1","transcript_path":"C:/t.jsonl","prompt":"oi"}""",
            _ => throw new InvalidOperationException("não lê"))!;
        Assert.Equal(TipoAviso.Comecou, a.Tipo);
        Assert.Equal("C:/t.jsonl", a.Transcricao);
        Assert.Equal("s1", a.ChaveSessao);
        Assert.Equal("vitral", a.Sessao);
        Assert.Equal("", a.Texto);
    }

    [Fact]
    public void Sem_session_id_a_chave_e_a_pasta()
    {
        var a = AvisoClaude.DeGancho("""{"hook_event_name":"UserPromptSubmit","cwd":"C:/p/vitral"}""", _ => null)!;
        Assert.Equal("vitral", a.ChaveSessao);
    }

    [Fact]
    public void Notification_guarda_o_tipo()
    {
        var a = AvisoClaude.DeGancho("""{"hook_event_name":"Notification","cwd":"C:/p/x","message":"oi","notification_type":"idle_prompt"}""", _ => null)!;
        Assert.Equal("idle_prompt", a.TipoNotificacao);
    }

    [Fact]
    public void Linha_de_ida_e_volta_leva_Transcricao_e_TipoNotificacao()
    {
        var a = new AvisoClaude(TipoAviso.Comecou, "x", "") { Transcricao = @"C:/t.jsonl", TipoNotificacao = "permission_prompt", SessaoId = "s" };
        var b = AvisoClaude.DeLinha(a.ParaLinha())!;
        Assert.Equal(TipoAviso.Comecou, b.Tipo);
        Assert.Equal(a.Transcricao, b.Transcricao);
        Assert.Equal("permission_prompt", b.TipoNotificacao);
    }

    [Fact]
    public void Interrompido_ve_a_marca_no_fim()
    {
        const string marca = """{"type":"user","message":{"role":"user","content":[{"type":"text","text":"[Request interrupted by user]"}]}}""";
        const string marcaTexto = """{"type":"user","message":{"role":"user","content":"[Request interrupted by user for tool use]"}}""";
        Assert.True(TurnoClaude.Interrompido(Usuario + "\n" + Assistente + "\n" + marca + "\n"));
        Assert.True(TurnoClaude.Interrompido(Usuario + "\n" + marcaTexto));
        Assert.False(TurnoClaude.Interrompido(marca + "\n" + Assistente));
        Assert.False(TurnoClaude.Interrompido(Usuario + "\n" + Assistente));
        Assert.False(TurnoClaude.Interrompido(""));
    }
}

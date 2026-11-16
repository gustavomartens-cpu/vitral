using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

/// Bloco 2 das ideias de notch (R4 e R5): de onde veio o turno que terminou e a ausência do Gustavo.
public sealed class AvisosCerteirosTests
{
    // Linhas no formato do Claude Code 2.1.325 (só os campos que interessam).
    private const string Digitada = """{"type":"user","isSidechain":false,"uuid":"m1","promptSource":"sdk","turnOrigin":"human","message":{"role":"user","content":"faz o commit"}}""";
    private const string DeTarefa = """{"type":"user","isSidechain":false,"uuid":"m2","promptSource":"system","turnOrigin":"task_notification","message":{"role":"user","content":"<task-notification>"}}""";
    private const string DeOutraSessao = """{"type":"user","isSidechain":false,"uuid":"m3","turnOrigin":"peer","message":{"role":"user","content":"oi"}}""";
    private const string NaFila = """{"type":"attachment","isSidechain":false,"uuid":"m4","attachment":{"type":"queued_command","prompt":"e o push","commandMode":"prompt","origin":{"kind":"human"},"humanTurn":true}}""";
    private const string NaFilaSemOrigem = """{"type":"attachment","uuid":"m5","attachment":{"type":"queued_command","prompt":"x","commandMode":"prompt"}}""";
    private const string Resultado = """{"type":"user","uuid":"r1","message":{"role":"user","content":[{"type":"tool_result","content":"\"turnOrigin\":\"human\" e \"queued_command\""}]}}""";
    private const string Assistente = """{"type":"assistant","uuid":"a1","message":{"role":"assistant","content":[{"type":"text","text":"Pronto."}]}}""";

    private static string J(params string[] linhas) => string.Join("\n", linhas) + "\n";

    [Fact]
    public void Turno_digitado_e_do_Gustavo()
    {
        var t = TurnoClaude.Ultimo(J(Digitada, Resultado, Assistente));
        Assert.Equal(new TurnoClaude("human", "m1"), t);
        Assert.True(t!.DoGustavo);
    }

    [Theory]
    [InlineData(DeTarefa, "task_notification")]
    [InlineData(DeOutraSessao, "peer")]
    public void Turno_automatico_nao_e_do_Gustavo(string abertura, string origem)
    {
        var t = TurnoClaude.Ultimo(J(Digitada, Assistente, abertura, Resultado, Assistente))!;
        Assert.Equal(origem, t.Origem);
        Assert.False(t.DoGustavo);
    }

    [Fact]
    public void Mensagem_digitada_no_meio_de_um_turno_automatico_conta()
    {
        var t = TurnoClaude.Ultimo(J(DeTarefa, Assistente, NaFila, Assistente))!;
        Assert.Equal(new TurnoClaude("human", "m4"), t);
    }

    [Fact]
    public void Fila_sem_origem_e_texto_de_ferramenta_nao_contam() =>
        Assert.Equal("m2", TurnoClaude.Ultimo(J(DeTarefa, NaFilaSemOrigem, Resultado, Assistente))!.Mensagem);

    [Fact]
    public void Sem_origem_no_trecho_e_null() =>
        Assert.Null(TurnoClaude.Ultimo(J("{cortada", Resultado, Assistente)));

    [Fact]
    public void Subagente_nao_conta()
    {
        var lateral = DeTarefa.Replace("\"isSidechain\":false", "\"isSidechain\":true");
        Assert.Equal("m1", TurnoClaude.Ultimo(J(Digitada, lateral))!.Mensagem);
    }

    private const string Stop = """{"hook_event_name":"Stop","cwd":"G:\\dev\\vitral","transcript_path":"t","session_id":"s1"}""";

    [Fact]
    public void Stop_leva_a_origem_e_a_mensagem()
    {
        var a = AvisoClaude.DeGancho(Stop, _ => J(DeTarefa, Assistente))!;
        Assert.Equal("task_notification", a.Origem);
        Assert.Equal("m2", a.Mensagem);
        Assert.True(a.DeOutraOrigem);
        Assert.False(AvisoClaude.DeGancho(Stop, _ => J(Digitada, Assistente))!.DeOutraOrigem);
    }

    [Fact]
    public void Turno_longo_le_mais_da_transcricao()
    {
        int maiores = 0;
        var a = AvisoClaude.DeGancho(Stop, _ => J(Resultado, Assistente), lerMais: _ => { maiores++; return J(Digitada, Resultado, Assistente); })!;
        Assert.Equal("m1", a.Mensagem);
        Assert.Equal(1, maiores);
        // Achou no primeiro trecho: não lê de novo.
        AvisoClaude.DeGancho(Stop, _ => J(Digitada, Assistente), lerMais: _ => { maiores++; return null; });
        Assert.Equal(1, maiores);
    }

    [Fact]
    public void Sem_origem_avisa_como_antes()
    {
        var a = AvisoClaude.DeGancho(Stop, _ => J(Assistente))!;
        Assert.Null(a.Origem);
        Assert.False(a.DeOutraOrigem);
    }

    [Fact]
    public void Notification_nao_le_a_origem()
    {
        var a = AvisoClaude.DeGancho("""{"hook_event_name":"Notification","message":"x","transcript_path":"t"}""", _ => J(DeTarefa, Assistente))!;
        Assert.Null(a.Origem);
        Assert.False(a.DeOutraOrigem);
    }

    [Fact]
    public void Origem_e_mensagem_viajam_pelo_canal()
    {
        var a = new AvisoClaude(TipoAviso.Terminou, "vitral", "ok") { Origem = "peer", Mensagem = "m3" };
        Assert.Equal(a, AvisoClaude.DeLinha(a.ParaLinha()));
        var r = PedidoResposta.DeAviso(a with { Origem = "human" }, new DateTime(2026, 11, 16))!;
        Assert.Equal("m3", r.Mensagem);
        Assert.Equal("m3", PedidoResposta.DeLinha(r.ParaLinha())!.Mensagem);
    }

    [Fact]
    public void Linha_antiga_sem_origem_continua_valendo() =>
        Assert.Null(AvisoClaude.DeLinha("""{"tipo":"Terminou","sessao":"v","texto":"ok"}""")!.Origem);

    [Fact]
    public void Turno_que_o_Gustavo_nao_pediu_nao_segura_o_Stop()
    {
        Assert.True(Segurar.NoStop(Stop, claudeNaFrente: false, sessaoComGente: true, ganchoComTempo: true, turnoDoGustavo: true));
        Assert.False(Segurar.NoStop(Stop, claudeNaFrente: false, sessaoComGente: true, ganchoComTempo: true, turnoDoGustavo: false));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0.0, false)]
    [InlineData(4.9, false)]
    [InlineData(5.0, true)]
    [InlineData(60.0, true)]
    public void Ausente_depois_de_cinco_minutos(double? minutos, bool ausente) =>
        Assert.Equal(ausente, Ausencia.Ausente(minutos is { } m ? TimeSpan.FromMinutes(m) : null));
}

using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

/// O Claude trabalhando e a música no notch fechado (spec do notch fechado §2.1 e §3.1).
public sealed class AtividadeClaudeTests
{
    private DateTime _agora = new(2026, 11, 16, 22, 0, 0);
    private AtividadeClaude Criar() => new(() => _agora);

    private static AvisoClaude Comecou(string sessao = "s1", string pasta = "vitral", string transcricao = "t1") =>
        new(TipoAviso.Comecou, pasta, "") { SessaoId = sessao, Transcricao = transcricao };

    private static AvisoClaude Terminou(string sessao = "s1", string? origem = TurnoClaude.Humano) =>
        new(TipoAviso.Terminou, "vitral", "pronto") { SessaoId = sessao, Origem = origem };

    private static AvisoClaude Esperando(string sessao = "s1", string? tipo = "permission_prompt") =>
        new(TipoAviso.Esperando, "vitral", "permissão") { SessaoId = sessao, TipoNotificacao = tipo };

    private static Func<string, LeituraTranscricao?> Leitura(DateTime escrita, bool interrompida = false) => _ => new(escrita, interrompida);

    [Fact]
    public void Comecou_conta_rodando()
    {
        var a = Criar();
        a.Chegou(Comecou());
        Assert.Equal(new ResumoAtividade(1, 0, false), a.Resumo);
    }

    [Fact]
    public void Duas_sessoes_na_mesma_pasta_contam_duas()
    {
        var a = Criar();
        a.Chegou(Comecou("s1"));
        a.Chegou(Comecou("s2"));
        Assert.Equal(2, a.Resumo.Rodando);
    }

    [Fact]
    public void Sem_SessaoId_a_pasta_e_a_chave()
    {
        var a = Criar();
        a.Chegou(new AvisoClaude(TipoAviso.Comecou, "vitral", ""));
        a.Chegou(new AvisoClaude(TipoAviso.Comecou, "vitral", ""));
        Assert.Equal(1, a.Resumo.Rodando);
    }

    [Fact]
    public void Stop_do_Gustavo_acende_o_check_por_3s()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.Chegou(Terminou());
        Assert.Equal(new ResumoAtividade(0, 0, true), a.Resumo);
        _agora = _agora.AddSeconds(2.9);
        a.Conferir(_ => null);
        Assert.True(a.Resumo.Terminou);
        _agora = _agora.AddSeconds(0.2);
        a.Conferir(_ => null);
        Assert.Equal(new ResumoAtividade(0, 0, false), a.Resumo);
        Assert.False(a.PrecisaConferir);
    }

    [Fact]
    public void Stop_de_outra_origem_tira_sem_check()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.Chegou(Terminou(origem: "task_notification"));
        Assert.Equal(new ResumoAtividade(0, 0, false), a.Resumo);
    }

    [Fact]
    public void Stop_sem_Comecou_nao_acende()
    {
        var a = Criar();
        a.Chegou(Terminou());
        Assert.Equal(new ResumoAtividade(0, 0, false), a.Resumo);
    }

    [Fact]
    public void Notification_de_pedido_pinta_esperando()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.Chegou(Esperando());
        Assert.Equal(new ResumoAtividade(0, 1, false), a.Resumo);
    }

    [Fact]
    public void Ocioso_depois_do_Stop_nao_pinta()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.Chegou(Terminou());
        a.Chegou(Esperando(tipo: "idle_prompt"));
        Assert.Equal(0, a.Resumo.Esperando);
        a.Chegou(Esperando()); // sessão que já terminou: nem o pedido pinta
        Assert.Equal(0, a.Resumo.Esperando);
    }

    [Fact]
    public void Ocioso_com_a_sessao_rodando_nao_pinta()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.Chegou(Esperando(tipo: "idle_prompt"));
        Assert.Equal(new ResumoAtividade(1, 0, false), a.Resumo);
    }

    [Fact]
    public void Pedido_aberto_e_fechado()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.PedidoAbriu("s1");
        Assert.Equal(new ResumoAtividade(0, 1, false), a.Resumo);
        a.PedidoSaiu("s1");
        Assert.Equal(new ResumoAtividade(1, 0, false), a.Resumo);
    }

    [Fact]
    public void Pedido_sem_Comecou_cria_a_sessao_esperando()
    {
        var a = Criar();
        a.PedidoAbriu("s9"); // Vitral aberto no meio do turno
        Assert.Equal(new ResumoAtividade(0, 1, false), a.Resumo);
        a.PedidoSaiu("s9");
        Assert.Equal(new ResumoAtividade(1, 0, false), a.Resumo);
    }

    [Fact]
    public void Transcricao_crescendo_tira_da_espera()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.Chegou(Esperando());
        var entrou = _agora;
        _agora = _agora.AddSeconds(10);
        a.Conferir(Leitura(entrou.AddSeconds(1))); // escrita logo depois do aviso: ainda esperando
        Assert.Equal(1, a.Resumo.Esperando);
        a.Conferir(Leitura(entrou.AddSeconds(5)));
        Assert.Equal(new ResumoAtividade(1, 0, false), a.Resumo);
    }

    [Fact]
    public void Transcricao_crescendo_nao_tira_pedido_aberto()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.PedidoAbriu("s1");
        _agora = _agora.AddSeconds(10);
        a.Conferir(Leitura(_agora));
        Assert.Equal(1, a.Resumo.Esperando);
    }

    [Fact]
    public void Interrompida_some_sem_check()
    {
        var a = Criar();
        a.Chegou(Comecou());
        a.Conferir(Leitura(_agora, interrompida: true));
        Assert.Equal(new ResumoAtividade(0, 0, false), a.Resumo);
    }

    [Fact]
    public void Trinta_minutos_parada_some()
    {
        var a = Criar();
        a.Chegou(Comecou());
        var escrita = _agora;
        _agora = _agora.AddMinutes(29);
        a.Conferir(Leitura(escrita));
        Assert.Equal(1, a.Resumo.Rodando);
        _agora = escrita.AddMinutes(30).AddSeconds(1);
        a.Conferir(Leitura(escrita));
        Assert.Equal(new ResumoAtividade(0, 0, false), a.Resumo);
    }

    [Fact]
    public void Sem_transcricao_legivel_conta_desde_o_comeco()
    {
        var a = Criar();
        a.Chegou(Comecou());
        _agora = _agora.AddMinutes(30).AddSeconds(1);
        a.Conferir(_ => null);
        Assert.Equal(0, a.Resumo.Rodando);
    }

    [Fact]
    public void Mudou_dispara_so_quando_o_resumo_muda()
    {
        var a = Criar();
        int vezes = 0;
        a.Mudou += () => vezes++;
        a.Chegou(Comecou());
        a.Chegou(Comecou()); // a mesma sessão de novo
        a.Conferir(Leitura(_agora));
        Assert.Equal(1, vezes);
        Assert.True(a.PrecisaConferir);
    }

    [Fact]
    public void Do_gancho_ate_a_contagem_pelo_canal()
    {
        static AvisoClaude PeloCanal(string json) => AvisoClaude.DeLinha(AvisoClaude.DeGancho(json, _ => null)!.ParaLinha())!;
        var a = Criar();
        a.Chegou(PeloCanal("""{"hook_event_name":"UserPromptSubmit","cwd":"G:/dev/demo","session_id":"demo1"}"""));
        a.Chegou(PeloCanal("""{"hook_event_name":"Notification","cwd":"G:/dev/demo","session_id":"demo1","message":"x","notification_type":"permission_prompt"}"""));
        Assert.Equal(new ResumoAtividade(0, 1, false), a.Resumo);
    }

    // Os lados do notch fechado (R8: ordem fixa)

    private static readonly EstadoMidia Musica = new("Lo-fi", "Lofi Girl", "Spotify", true, null);

    [Fact]
    public void Esperando_ganha_de_rodando() =>
        Assert.Equal(new Lados(LadoEsquerdo.Esperando, 1, false), LadosNotch.Decidir(new(2, 1, false), null, true));

    [Fact]
    public void Rodando_com_musica() =>
        Assert.Equal(new Lados(LadoEsquerdo.Rodando, 2, true), LadosNotch.Decidir(new(2, 0, false), Musica, true));

    [Fact]
    public void So_musica_deixa_a_esquerda_vazia() // a capa fica à direita, com as barras (Gustavo, 16/11/2026)
    {
        var lados = LadosNotch.Decidir(new(0, 0, false), Musica, true);
        Assert.Equal(new Lados(LadoEsquerdo.Nada, 0, true), lados);
        Assert.True(lados.Algum);
    }

    [Fact]
    public void Musica_desligada_no_perfil() =>
        Assert.Equal(new Lados(LadoEsquerdo.Nada, 0, false), LadosNotch.Decidir(new(0, 0, false), Musica, false));

    [Fact]
    public void Terminou_com_musica() =>
        Assert.Equal(new Lados(LadoEsquerdo.Terminou, 0, true), LadosNotch.Decidir(new(0, 0, true), Musica, true));

    [Fact]
    public void Rodando_ganha_do_check() =>
        Assert.Equal(LadoEsquerdo.Rodando, LadosNotch.Decidir(new(1, 0, true), null, true).Esquerdo);

    [Fact]
    public void Nada()
    {
        var lados = LadosNotch.Decidir(new(0, 0, false), null, true);
        Assert.Equal(new Lados(LadoEsquerdo.Nada, 0, false), lados);
        Assert.False(lados.Algum);
    }
}

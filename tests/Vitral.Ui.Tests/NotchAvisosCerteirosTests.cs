using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

/// Bloco 2 das ideias de notch: só avisa o que o Gustavo pediu, uma vez por mensagem (R4), e lembra a pergunta
/// esquecida (R7).
public sealed class NotchAvisosCerteirosTests
{
    private DateTime _agora = new(2026, 11, 16, 20, 0, 0);
    private EstadoNotch Criar() => new(() => _agora);
    private void Avancar(double segundos, EstadoNotch e) { _agora = _agora.AddSeconds(segundos); e.Tique(); }

    private static AvisoClaude Fim(string? origem, string? mensagem, string sessao = "s1") =>
        new(TipoAviso.Terminou, "vitral", "Pronto.") { SessaoId = sessao, Origem = origem, Mensagem = mensagem };

    private static PedidoPermissao Permissao(string id, string sessao = "s1") =>
        new(id, "vitral", "Bash", "Claude quer rodar um comando", "npm test") { SessaoId = sessao };

    [Fact]
    public void Fim_de_mensagem_digitada_desce()
    {
        var e = Criar();
        e.Chegou(Fim("human", "m1"));
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Assert.True(e.Pendente);
    }

    [Theory]
    [InlineData("task_notification")]
    [InlineData("peer")]
    [InlineData("host_synthetic")]
    [InlineData("sdk")]
    [InlineData("scheduled")]
    public void Fim_de_turno_automatico_nao_desce(string origem)
    {
        var e = Criar();
        e.Chegou(Fim(origem, "m2"));
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.False(e.Pendente);
        Assert.Null(e.Aviso);
    }

    [Fact]
    public void Fim_sem_origem_desce_como_antes()
    {
        var e = Criar();
        e.Chegou(Fim(null, null));
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Fim_automatico_ainda_tira_o_pedido_da_sessao()
    {
        var e = Criar();
        var encerrados = new List<string>();
        e.Encerrado += (p, _) => encerrados.Add(p.Id);
        e.Pediu(Permissao("p1"));
        e.Chegou(Fim("task_notification", "m2"));
        Assert.Equal(["p1"], encerrados);
        Assert.Equal(ModoNotch.Normal, e.Modo); // resolvido no app, sem "pedido sem resposta"
        Assert.False(e.PedidoPerdido);
    }

    [Fact]
    public void Uma_vez_por_mensagem()
    {
        var e = Criar();
        e.Chegou(Fim("human", "m1"));
        Avancar(EstadoNotch.Duracao, e);
        e.Clicou(ParteNotch.Esquerda); // viu
        Assert.Equal(ModoNotch.Normal, e.Modo);
        e.Chegou(Fim("human", "m1")); // outro gancho Stop devolveu o Claude ao trabalho e o mesmo turno terminou de novo
        Assert.Equal(ModoNotch.Normal, e.Modo);
        e.Chegou(Fim("human", "m2")); // mensagem nova avisa
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Mesma_mensagem_em_outra_sessao_avisa()
    {
        var e = Criar();
        e.Chegou(Fim("human", "m1", "s1"));
        Avancar(EstadoNotch.Duracao, e);
        e.Chegou(Fim("human", "m1", "s2"));
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Aviso_a_vista_nao_conta_como_avisado()
    {
        var e = Criar();
        e.Chegou(Fim("human", "m1") with { AVista = true });
        Assert.Equal(ModoNotch.Normal, e.Modo);
        e.Chegou(Fim("human", "m1")); // o Gustavo saiu da conversa antes do fim seguinte
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Fim_calado_nao_tira_a_pendencia()
    {
        var e = Criar();
        var pedido = new PedidoResposta("r1", "vitral", "Pronto.", _agora) { SessaoId = "s1", Mensagem = "m1" };
        e.SemNotch(pedido);
        Assert.Equal(1, e.Pendencias.Quantas);
        e.Chegou(Fim("task_notification", "m2"));
        Assert.Equal(1, e.Pendencias.Quantas); // a sessão continua esperando o Gustavo
        e.Chegou(Fim("human", "m3"));
        Assert.Equal(0, e.Pendencias.Quantas);
    }

    [Fact]
    public void Ficha_uma_vez_por_mensagem_e_de_novo_depois_da_resposta()
    {
        var e = Criar();
        PedidoResposta Ficha(string id) => new(id, "vitral", "Pronto.", _agora) { SessaoId = "s1", Mensagem = "m1" };
        Assert.True(e.PediuResposta(Ficha("r1")));
        Avancar(EstadoNotch.EsperaClique, e);
        e.Cancelou("r1");
        Assert.False(e.PediuResposta(Ficha("r2"))); // o mesmo turno de novo: sem ficha
        Assert.NotEqual(ModoNotch.Resposta, e.Modo);
        Assert.Equal(1, e.Pendencias.Quantas); // a do Esc continua

        // Respondida pelo notch, o texto continua o turno: o fim seguinte, com a mesma mensagem, ganha ficha.
        var outra = Criar();
        Assert.True(outra.PediuResposta(Ficha("r3")));
        Avancar(EstadoNotch.EsperaClique, outra);
        outra.Digitou("r3", "agora faz o push");
        Assert.True(outra.Enviou("r3"));
        Avancar(EstadoNotch.TempoConfirmacao, outra);
        Assert.True(outra.PediuResposta(Ficha("r4")));
        Assert.Equal("r4", outra.RespostaAtual!.Id);
    }

    private static PedidoPermissao Pergunta(string id) =>
        new(id, "vitral", RegrasPerguntas.Ferramenta, "Claude pergunta", "Qual fruta?")
        {
            SessaoId = "s1",
            Perguntas = [new("Qual fruta?", "Fruta", [new("Banana", null), new("Maçã", null)], false)],
        };

    private (EstadoNotch, List<string>) ComPergunta()
    {
        var e = Criar();
        var lembradas = new List<string>();
        e.PerguntaEsquecida += p => lembradas.Add(p.Id);
        e.Pediu(Pergunta("q1"));
        return (e, lembradas);
    }

    [Fact]
    public void Pergunta_esquecida_e_lembrada_uma_vez()
    {
        var (e, lembradas) = ComPergunta();
        Avancar(EstadoNotch.LembretePergunta - 1, e);
        Assert.Empty(lembradas);
        Avancar(1, e);
        Assert.Equal(["q1"], lembradas);
        Avancar(60, e);
        Assert.Equal(["q1"], lembradas);
        Assert.Equal(ModoNotch.Permissao, e.Modo); // a pergunta continua na ficha
    }

    [Fact]
    public void Com_o_mouse_em_cima_espera_sair()
    {
        var (e, lembradas) = ComPergunta();
        e.MouseEntrou();
        Avancar(EstadoNotch.LembretePergunta + 10, e);
        Assert.Empty(lembradas);
        e.MouseSaiu();
        Avancar(0.25, e);
        Assert.Equal(["q1"], lembradas);
    }

    [Fact]
    public void Nao_perturbe_cala_o_lembrete()
    {
        var (e, lembradas) = ComPergunta();
        e.NaoPerturbe = true;
        Avancar(EstadoNotch.LembretePergunta, e);
        e.NaoPerturbe = false;
        Avancar(10, e);
        Assert.Empty(lembradas); // a vez dela passou
    }

    [Fact]
    public void Permissao_nao_ganha_lembrete()
    {
        var e = Criar();
        var lembradas = new List<string>();
        e.PerguntaEsquecida += p => lembradas.Add(p.Id);
        e.Pediu(Permissao("p1"));
        Avancar(EstadoNotch.TempoPedido - 1, e);
        Assert.Empty(lembradas);
    }

    [Fact]
    public void Pergunta_atras_de_permissao_e_lembrada_quando_volta()
    {
        var (e, lembradas) = ComPergunta();
        Avancar(80, e);
        e.Pediu(Permissao("p1", "s2")); // passa na frente
        Avancar(20, e); // a pergunta já tem 100 s, mas está atrás
        Assert.Empty(lembradas);
        Avancar(10, e); // a permissão vence e a pergunta volta
        Assert.Equal(["q1"], lembradas);
    }
}

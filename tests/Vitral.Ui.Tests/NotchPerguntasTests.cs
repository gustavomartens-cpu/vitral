using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

/// A ficha de pergunta do notch (spec das perguntas §3 e §4).
public sealed class NotchPerguntasTests
{
    private DateTime _agora = new(2026, 11, 3, 21, 0, 0);
    private readonly List<(PedidoPermissao Pedido, RespostaPedido? Resposta)> _encerrados = [];

    private EstadoNotch Criar()
    {
        var e = new EstadoNotch(() => _agora);
        e.Encerrado += (p, r) => _encerrados.Add((p, r));
        return e;
    }

    private void Avancar(double segundos, EstadoNotch e) { _agora = _agora.AddSeconds(segundos); e.Tique(); }

    private static readonly Pergunta Fruta = new("Qual fruta?", "Fruta", [new("Banana", null), new("Maçã", null)], false);
    private static readonly Pergunta Cores = new("Quais cores?", "Cores", [new("Azul", null), new("Verde", null), new("Roxo", null)], true);

    private static PedidoPermissao Pedido(string id, params Pergunta[] perguntas) =>
        new(id, "vitral", RegrasPerguntas.Ferramenta, "Claude pergunta", perguntas[0].Texto) { Perguntas = perguntas, SessaoId = "s1" };

    /// Coloca o pedido na ficha e passa a espera do primeiro clique.
    private EstadoNotch ComPergunta(params Pergunta[] perguntas)
    {
        var e = Criar();
        e.Pediu(Pedido("p1", perguntas));
        Avancar(EstadoNotch.EsperaClique, e);
        return e;
    }

    [Fact]
    public void Escolha_unica_responde()
    {
        var e = ComPergunta(Fruta);
        Assert.True(e.Escolheu("p1", "Maçã"));
        var (_, r) = Assert.Single(_encerrados);
        Assert.Equal(EscolhaPermissao.Permitir, r!.Escolha);
        Assert.Equal("Maçã", r.Respostas!["Qual fruta?"]);
    }

    [Fact]
    public void Duas_perguntas_acumulam()
    {
        var e = ComPergunta(Fruta, Cores);
        Assert.Equal(0, e.PerguntaAtual);
        Assert.True(e.Escolheu("p1", "Banana"));
        Assert.Empty(_encerrados); // a primeira não envia nada
        Assert.Equal(1, e.PerguntaAtual);
        Avancar(EstadoNotch.EsperaClique, e);
        e.Marcar("p1", "Roxo");
        e.Marcar("p1", "Azul");
        Assert.True(e.Enviar("p1"));
        var (_, r) = Assert.Single(_encerrados);
        Assert.Equal("Banana", r!.Respostas!["Qual fruta?"]);
        Assert.Equal("Azul, Roxo", r.Respostas["Quais cores?"]);
    }

    [Fact]
    public void Multipla_marca_desmarca_e_envia()
    {
        var e = ComPergunta(Cores);
        Assert.False(e.PodeEnviar);
        Assert.False(e.Enviar("p1"));
        e.Marcar("p1", "Verde");
        Assert.True(e.PodeEnviar);
        Assert.Contains("Verde", e.Marcadas);
        e.Marcar("p1", "Verde");
        Assert.False(e.PodeEnviar);
        Assert.False(e.Escolheu("p1", "Verde")); // na múltipla, o clique só marca
        e.Marcar("p1", "Azul");
        Assert.True(e.Enviar("p1"));
        Assert.Equal("Azul", Assert.Single(_encerrados).Resposta!.Respostas!["Quais cores?"]);
    }

    [Fact]
    public void Pergunta_dura_5_minutos()
    {
        var e = ComPergunta(Fruta);
        Avancar(61, e);
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        Assert.Empty(_encerrados);
        Avancar(EstadoNotch.TempoPergunta - 61, e);
        var (_, r) = Assert.Single(_encerrados);
        Assert.Null(r);
    }

    [Fact]
    public void Pergunta_respondida_no_app_sai()
    {
        var e = ComPergunta(Fruta, Cores);
        e.Escolheu("p1", "Banana");
        e.Desistiu("p1");
        Assert.Null(Assert.Single(_encerrados).Resposta);
        Assert.False(e.PedidoPerdido);
        Assert.NotEqual(ModoNotch.Permissao, e.Modo);
    }

    [Fact]
    public void Conversa_encerra_com_conversa()
    {
        var e = ComPergunta(Fruta);
        Assert.NotNull(e.Respondeu("p1", EscolhaPermissao.Conversa));
        Assert.Equal(EscolhaPermissao.Conversa, Assert.Single(_encerrados).Resposta!.Escolha);
    }

    [Fact]
    public void Clique_logo_ao_aparecer_e_ignorado()
    {
        var e = Criar();
        e.Pediu(Pedido("p1", Fruta, Cores));
        Assert.False(e.Escolheu("p1", "Banana"));
        Avancar(EstadoNotch.EsperaClique, e);
        Assert.False(e.Escolheu("outro", "Banana")); // outro pedido
        Assert.True(e.Escolheu("p1", "Banana"));
        e.Marcar("p1", "Azul"); // logo depois de trocar de pergunta: também ignorado
        Assert.Empty(e.Marcadas);
    }

    [Fact]
    public void Proximo_pedido_comeca_da_primeira_pergunta()
    {
        var e = Criar();
        e.Pediu(Pedido("p1", Fruta, Cores));
        e.Pediu(Pedido("p2", Fruta, Cores));
        Avancar(EstadoNotch.EsperaClique, e);
        e.Escolheu("p1", "Banana");
        e.Desistiu("p1");
        Assert.Equal("p2", e.PedidoAtual!.Id);
        Assert.Equal(0, e.PerguntaAtual);
        Assert.Empty(e.Marcadas);
    }
}

using System.Text.Json.Nodes;
using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

public sealed class RespostaTests
{
    private static readonly DateTime Hora = new(2026, 10, 26, 14, 32, 0);

    [Fact]
    public void Pedido_de_resposta_ida_e_volta()
    {
        var aviso = new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto.") { SessaoId = "s1", Completo = "Pronto.\n\nTudo certo." };
        var p = PedidoResposta.DeAviso(aviso, Hora)!;
        Assert.Equal("vitral", p.Sessao);
        Assert.Equal("s1", p.SessaoId);
        Assert.Equal("Pronto.\n\nTudo certo.", p.Completo);
        Assert.False(string.IsNullOrEmpty(p.Id));
        Assert.Equal(p, PedidoResposta.DeLinha(p.ParaLinha()));
        Assert.True(PedidoResposta.EhPedido(p.ParaLinha()));
        Assert.False(PedidoPermissao.EhPedido(p.ParaLinha()));
        Assert.Null(AvisoClaude.DeLinha(p.ParaLinha()));
    }

    [Fact]
    public void Pedido_sem_completo_usa_o_resumo() =>
        Assert.Equal("Pronto.", PedidoResposta.DeAviso(new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto."), Hora)!.Completo);

    [Fact]
    public void Resposta_com_id_errado_ou_vazia_e_null()
    {
        Assert.Equal("pode seguir", RespostaTexto.DaLinha(RespostaTexto.Linha("a", "pode seguir"), "a"));
        Assert.Contains("\"texto\":", RespostaTexto.Linha("a", "x"));
        Assert.Null(RespostaTexto.DaLinha(RespostaTexto.Linha("a", "pode seguir"), "b"));
        Assert.Null(RespostaTexto.DaLinha(RespostaTexto.Linha("a", "   "), "a"));
        Assert.Null(RespostaTexto.DaLinha("{", "a"));
    }

    [Fact]
    public void ParaClaude_preserva_aspas_e_quebras()
    {
        const string texto = "faz a \"B\"\nC:\\x";
        var j = JsonNode.Parse(RespostaTexto.ParaClaude(texto))!;
        Assert.Equal("block", (string?)j["decision"]);
        Assert.Equal(texto, (string?)j["reason"]);
    }

    private const string Stop = """{"hook_event_name":"Stop","cwd":"G:\\dev\\vitral"}""";
    private const string Notificacao = """{"hook_event_name":"Notification","cwd":"G:\\dev\\vitral","message":"oi"}""";

    [Fact]
    public void Nao_segura_com_o_Claude_na_frente() => Assert.False(Segurar.NoStop(Stop, claudeNaFrente: true, sessaoComGente: true, ganchoComTempo: true));

    [Fact]
    public void Segura_o_Stop_fora_do_app() => Assert.True(Segurar.NoStop(Stop, claudeNaFrente: false, sessaoComGente: true, ganchoComTempo: true));

    [Fact]
    public void Notification_nunca_segura() => Assert.False(Segurar.NoStop(Notificacao, claudeNaFrente: false, sessaoComGente: true, ganchoComTempo: true));

    [Fact]
    public void Sessao_sem_gente_nao_segura() => // claude -p, tarefa agendada, SDK
        Assert.False(Segurar.NoStop(Stop, claudeNaFrente: false, sessaoComGente: false, ganchoComTempo: true));

    [Fact]
    public void Gancho_sem_o_tempo_da_resposta_nao_segura() => // caixa ainda não religada depois da atualização
        Assert.False(Segurar.NoStop(Stop, claudeNaFrente: false, sessaoComGente: true, ganchoComTempo: false));

    [Theory]
    [InlineData("claude-desktop", "1", true)]
    [InlineData("claude-desktop", null, true)]
    [InlineData("claude-desktop", "0", false)]
    [InlineData("sdk-cli", "1", false)]
    [InlineData(null, null, false)]
    public void Sessao_com_gente_pelo_ambiente(string? entrada, string? atendida, bool esperado) =>
        Assert.Equal(esperado, Segurar.SessaoComGente(entrada, atendida));

    [Fact]
    public void Espera_da_resposta_cabe_no_timeout_do_gancho() =>
        Assert.True(Canal.EsperaRespostaMs == 1_140_000 && Canal.EsperaRespostaMs < GanchosClaude.TempoResposta * 1000);
}

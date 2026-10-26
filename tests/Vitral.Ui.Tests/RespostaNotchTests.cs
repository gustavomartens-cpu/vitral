using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class RespostaNotchTests
{
    private DateTime _agora = new(2026, 10, 26, 14, 0, 0);
    private EstadoNotch Criar() => new(() => _agora);
    private void Avancar(double segundos, EstadoNotch e) { _agora = _agora.AddSeconds(segundos); e.Tique(); }

    private PedidoResposta R(string id, string sessao = "vitral") => new(id, sessao, "Pronto.\n\nTudo certo.", _agora) { SessaoId = "s-" + sessao };
    private static PedidoPermissao P(string id) => new(id, "sic", "Bash", "Claude quer rodar um comando", "npm test");

    private static List<(string Id, string? Texto)> Encerradas(EstadoNotch e)
    {
        var l = new List<(string, string?)>();
        e.RespostaEncerrada += (p, t) => l.Add((p.Id, t));
        return l;
    }

    [Fact]
    public void Pedido_de_resposta_abre_a_ficha()
    {
        var e = Criar();
        e.PediuResposta(R("1"));
        Assert.Equal(ModoNotch.Resposta, e.Modo);
        Assert.Equal("1", e.RespostaAtual!.Id);
        Assert.Equal(_agora, e.ChegadaDaResposta);
        Assert.False(e.Digitando);
    }

    [Fact]
    public void Sessenta_segundos_sem_digitar_encerra_sem_texto_e_vai_para_as_pendencias()
    {
        var e = Criar();
        var fim = Encerradas(e);
        e.PediuResposta(R("1"));
        Avancar(59.9, e);
        Assert.Equal(ModoNotch.Resposta, e.Modo);
        Avancar(0.2, e);
        Assert.Equal([("1", (string?)null)], fim);
        Assert.Equal(1, e.Pendencias.Quantas);
        Assert.Equal("Pronto.\n\nTudo certo.", e.Pendencias.Itens[0].Completo);
        Assert.NotEqual(ModoNotch.Resposta, e.Modo);
    }

    [Fact]
    public void Digitar_para_o_tempo_e_vale_o_teto_de_15_minutos()
    {
        var e = Criar();
        var fim = Encerradas(e);
        e.PediuResposta(R("1"));
        Avancar(30, e);
        e.Digitou("1", "p");
        Assert.True(e.Digitando);
        Avancar(31, e);
        Assert.Equal(ModoNotch.Resposta, e.Modo);
        Avancar(EstadoNotch.TetoDigitando - 31.1, e);
        Assert.Empty(fim);
        Avancar(0.2, e);
        Assert.Equal([("1", (string?)null)], fim);
        Assert.Equal(1, e.Pendencias.Quantas);
    }

    [Fact]
    public void Enviar_encerra_com_o_texto_e_confirma()
    {
        var e = Criar();
        var fim = Encerradas(e);
        e.PediuResposta(R("1"));
        Avancar(0.6, e);
        e.Digitou("1", "pode seguir");
        Assert.True(e.Enviou("1"));
        Assert.Equal([("1", (string?)"pode seguir")], fim);
        Assert.True(e.ConfirmandoResposta);
        Avancar(1.1, e);
        Assert.False(e.ConfirmandoResposta);
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.Equal(0, e.Pendencias.Quantas);
    }

    [Fact]
    public void Enviar_vazio_ou_logo_que_aparece_e_recusado()
    {
        var e = Criar();
        e.PediuResposta(R("1"));
        e.Digitou("1", "sim");
        Assert.False(e.Enviou("1")); // antes da EsperaClique
        Avancar(0.6, e);
        e.Digitou("1", "   ");
        Assert.False(e.Enviou("1"));
        Assert.Equal(ModoNotch.Resposta, e.Modo);
    }

    [Fact]
    public void Esc_cancela_e_vai_para_as_pendencias()
    {
        var e = Criar();
        var fim = Encerradas(e);
        e.PediuResposta(R("1"));
        e.Cancelou("1");
        Assert.Equal([("1", (string?)null)], fim);
        Assert.Equal(1, e.Pendencias.Quantas);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Desistir_tira_sem_ir_para_as_pendencias()
    {
        var e = Criar();
        e.PediuResposta(R("1"));
        e.DesistiuResposta("1");
        Assert.Equal(0, e.Pendencias.Quantas);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Aviso_da_mesma_sessao_descarta_a_ficha()
    {
        var e = Criar();
        e.PediuResposta(R("1"));
        e.Chegou(new AvisoClaude(TipoAviso.Terminou, "vitral", "ok") { SessaoId = "s-vitral" });
        Assert.Null(e.RespostaAtual);
        Assert.Equal(0, e.Pendencias.Quantas);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Fila_de_respostas_com_mais_um()
    {
        var e = Criar();
        e.PediuResposta(R("1"));
        e.PediuResposta(R("2", "sic"));
        Assert.Equal(1, e.NaFilaResposta);
        e.Cancelou("1");
        Assert.Equal("2", e.RespostaAtual!.Id);
        Assert.Equal(0, e.NaFilaResposta);
    }

    [Fact]
    public void Permissao_passa_a_frente_e_a_resposta_volta_com_o_texto()
    {
        var e = Criar();
        e.PediuResposta(R("1"));
        e.Digitou("1", "pode");
        e.Pediu(P("p"));
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        Avancar(0.6, e);
        e.Respondeu("p", EscolhaPermissao.Permitir);
        Avancar(1.1, e);
        Assert.Equal(ModoNotch.Resposta, e.Modo);
        Assert.Equal("pode", e.Rascunho);
        Assert.True(e.Digitando);
    }

    [Fact]
    public void Esperando_fora_do_app_entra_nas_pendencias()
    {
        var e = Criar();
        e.Chegou(new AvisoClaude(TipoAviso.Esperando, "sic", "esperando") { SessaoId = "s-sic", Completo = "Qual das duas?", ForaDoApp = true });
        e.Chegou(new AvisoClaude(TipoAviso.Esperando, "crm", "esperando") { SessaoId = "s-crm" });
        Assert.Equal(1, e.Pendencias.Quantas);
        Assert.Equal(TipoAviso.Esperando, e.Pendencias.Itens[0].Tipo);
        Assert.Equal("Qual das duas?", e.Pendencias.Itens[0].Completo);
    }

    [Fact]
    public void Pendencia_nova_da_mesma_sessao_substitui_a_antiga()
    {
        var e = Criar();
        e.PediuResposta(R("1"));
        e.Cancelou("1");
        e.PediuResposta(R("2", "sic"));
        e.Cancelou("2");
        Assert.Equal(2, e.Pendencias.Quantas);
        Assert.Equal("sic", e.Pendencias.Itens[0].Sessao); // a mais nova em cima
        e.PediuResposta(R("3")); // a sessão vitral terminou de novo: a pendência antiga dela sai
        Assert.Equal(1, e.Pendencias.Quantas);
        e.Cancelou("3");
        Assert.Equal(2, e.Pendencias.Quantas);
        Assert.Equal("vitral", e.Pendencias.Itens[0].Sessao);
    }

    [Fact]
    public void Sem_notch_visivel_vai_direto_para_as_pendencias()
    {
        var e = Criar();
        e.SemNotch(R("1"));
        Assert.Equal(1, e.Pendencias.Quantas);
        Assert.Null(e.RespostaAtual);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }
}

using Vitral.Core.Claude;
using Vitral.Core.Notificacoes;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class NotchNotificacoesTests
{
    private DateTime _agora = new(2026, 11, 2, 10, 0, 0);
    private EstadoNotch Criar() => new(() => _agora);
    private void Avancar(double segundos, EstadoNotch e) { _agora = _agora.AddSeconds(segundos); e.Tique(); }

    private static AvisoNotificacao Chrome(string titulo = "Resultados") => new("Chrome", "Google Chrome", titulo, "4 leads");

    [Fact]
    public void Clique_na_rajada_abre_o_painel()
    {
        // "5 notificações novas" não é da primeira: o clique mostra todas no painel do sino (sobras das notificações).
        var e = Criar();
        e.Notificou(new AvisoNotificacao("Chrome", "Google Chrome", "5 notificações novas", null, 5));
        Assert.Equal(AcaoNotch.AbrirPainelNotificacoes, e.Clicou());
        Assert.Null(e.AppPedido);
        Assert.Null(e.AvisoNotificacaoAtual);
    }

    [Theory]
    [InlineData("Resultados", "4 leads", "Google Chrome · Resultados", "4 leads")]
    [InlineData(null, "4 leads", "Google Chrome · 4 leads", "")]
    [InlineData("  ", null, "Google Chrome", "")]
    public void Linha_do_aviso_sem_titulo_vazio(string? titulo, string? texto, string linha, string detalhe)
    {
        var a = new AvisoNotificacao("Chrome", "Google Chrome", titulo, texto);
        Assert.Equal((linha, detalhe), (a.Linha, a.Detalhe));
    }

    [Fact]
    public void Linha_da_rajada()
    {
        var a = new AvisoNotificacao("Chrome", "Google Chrome", "5 notificações novas", null, 5);
        Assert.Equal(("5 notificações novas", "clique para ver todas"), (a.Linha, a.Detalhe));
    }

    [Fact]
    public void Notificacao_desce_e_sai_em_6s()
    {
        var e = Criar();
        e.Notificou(Chrome());
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Assert.Equal("Resultados", e.AvisoNotificacaoAtual!.Titulo);
        Avancar(6.1, e);
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.Null(e.AvisoNotificacaoAtual);
    }

    [Fact]
    public void Espera_a_ficha_de_permissao()
    {
        var e = Criar();
        e.Pediu(new PedidoPermissao("p1", "vitral", "Bash", "Claude quer rodar um comando", "ls"));
        e.Notificou(Chrome());
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        e.Desistiu("p1");
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Assert.NotNull(e.AvisoNotificacaoAtual);
    }

    [Fact]
    public void Fila_com_aviso_do_Claude_por_chegada()
    {
        var e = Criar();
        e.Chegou(new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto."));
        e.Notificou(Chrome("Primeira"));
        e.Notificou(Chrome("Segunda"));
        Assert.Null(e.AvisoNotificacaoAtual); // o do Claude continua na tela
        Avancar(6.1, e);
        Assert.Equal("Primeira", e.AvisoNotificacaoAtual!.Titulo);
        Avancar(6.1, e);
        Assert.Equal("Segunda", e.AvisoNotificacaoAtual!.Titulo);
        Avancar(6.1, e);
        Assert.Equal(ModoNotch.Lembrete, e.Modo); // o do Claude ficou pendente
    }

    [Fact]
    public void Clique_no_aviso_abre_o_app()
    {
        var e = Criar();
        e.Notificou(Chrome());
        Assert.Equal(AcaoNotch.AbrirApp, e.Clicou());
        Assert.Equal("Chrome", e.AppPedido);
        Assert.Null(e.AvisoNotificacaoAtual);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Sino_abre_o_painel_quando_ligado()
    {
        var e = Criar();
        e.NotificacoesLigadas = true;
        Assert.Equal(AcaoNotch.AbrirPainelNotificacoes, e.Clicou(ParteNotch.Direita));
    }

    [Fact]
    public void Sem_identidade_o_sino_abre_o_painel_que_explica()
    {
        var e = Criar();
        e.NotificacoesLigadas = false;
        Assert.Equal(AcaoNotch.AbrirPainelNotificacoes, e.Clicou(ParteNotch.Direita));
    }

    [Fact]
    public void Notificacao_interrompida_por_ficha_volta_e_nao_fica_presa()
    {
        var e = Criar();
        e.Notificou(Chrome("Interrompida"));
        e.Pediu(new PedidoPermissao("p1", "vitral", "Bash", "Claude quer rodar um comando", "ls"));
        Assert.Null(e.AvisoNotificacaoAtual); // a ficha tomou a tela
        e.Desistiu("p1");
        Assert.Equal("Interrompida", e.AvisoNotificacaoAtual!.Titulo); // volta depois da ficha
        Avancar(6.1, e);
        Assert.Null(e.AvisoNotificacaoAtual);
    }

    [Fact]
    public void Rajada_vira_um_aviso_so()
    {
        NotificacaoApp N(uint id) => new(id, "Chrome", "Google Chrome", $"T{id}", null, DateTimeOffset.Now);
        Assert.Equal(3, AvisoNotificacao.De([N(1), N(2), N(3)]).Count);
        var rajada = Assert.Single(AvisoNotificacao.De([N(1), N(2), N(3), N(4)]));
        Assert.Equal(4, rajada.Quantas);
        Assert.Equal("4 notificações novas", rajada.Titulo);
    }
}

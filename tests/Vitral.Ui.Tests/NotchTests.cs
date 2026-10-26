using System.Globalization;
using System.Text.RegularExpressions;
using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class NotchTests
{
    private DateTime _agora = new(2026, 10, 25, 21, 0, 0);
    private EstadoNotch Criar() => new(() => _agora);
    private void Avancar(double segundos, EstadoNotch e) { _agora = _agora.AddSeconds(segundos); e.Tique(); }

    private static readonly AvisoClaude A1 = new(TipoAviso.Terminou, "vitral", "Pronto.");
    private static readonly AvisoClaude A2 = new(TipoAviso.Esperando, "sic", "Posso seguir?");

    [Fact]
    public void Caminho_comeca_no_topo_e_fecha()
    {
        var c = FormaNotch.Caminho(200, 34, 16);
        Assert.StartsWith("M0,0", c);
        Assert.EndsWith("Z", c);
        Assert.Contains("A16,16", c);
        Assert.Contains("A22,22", FormaNotch.Caminho(300, 52, 22));
    }

    [Fact]
    public void Caminho_tem_a_largura_total()
    {
        var xs = Regex.Matches(FormaNotch.Caminho(200, 34, 16), @"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)")
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        Assert.Contains(232d, xs);
        Assert.True(xs.Max() <= 232);
    }

    [Fact]
    public void Aviso_depois_de_6s_vira_lembrete()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(5.9, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Avancar(0.2, e);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
    }

    [Fact]
    public void Clique_no_aviso_abre_o_Claude_e_volta_ao_normal()
    {
        var e = Criar();
        e.Chegou(A1);
        Assert.Equal(AcaoNotch.AbrirClaude, e.Clicou());
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Avancar(10, e);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Aviso_novo_reinicia_o_tempo()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(5, e);
        e.Chegou(A2);
        Avancar(5, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Assert.Same(A2, e.Aviso);
    }

    [Fact]
    public void Mouse_em_cima_segura_o_aviso()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(1, e);
        e.MouseEntrou();
        Avancar(19, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        e.MouseSaiu();
        Avancar(5.9, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Avancar(0.2, e);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
    }

    [Fact]
    public void Clique_no_lembrete_abre_o_calendario_e_limpa()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(7, e);
        Assert.Equal(AcaoNotch.AbrirCalendario, e.Clicou());
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.Equal(AcaoNotch.AbrirCalendario, e.Clicou());
    }

    [Fact]
    public void Mudou_dispara_em_cada_troca_de_modo()
    {
        var e = Criar();
        var modos = new List<ModoNotch>();
        e.Mudou += () => modos.Add(e.Modo);
        e.Chegou(A1);
        Avancar(7, e);
        e.Clicou();
        Assert.Equal([ModoNotch.Aviso, ModoNotch.Lembrete, ModoNotch.Normal], modos);
    }

    [Fact]
    public void Mouse_parado_250ms_abre_as_laterais()
    {
        var e = Criar();
        int mudancas = 0;
        e.Mudou += () => mudancas++;
        e.MouseEntrou();
        Avancar(0.2, e);
        Assert.False(e.Laterais);
        Avancar(0.1, e);
        Assert.True(e.Laterais);
        Assert.Equal(1, mudancas);
    }

    [Fact]
    public void Sair_recolhe_as_laterais()
    {
        var e = Criar();
        e.MouseEntrou();
        Avancar(0.3, e);
        e.MouseSaiu();
        Assert.False(e.Laterais);
    }

    [Fact]
    public void No_aviso_o_mouse_nao_abre_laterais()
    {
        var e = Criar();
        e.Chegou(A1);
        e.MouseEntrou();
        Avancar(1, e);
        Assert.False(e.Laterais);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Clique_na_esquerda_abre_o_Claude_e_limpa_o_lembrete()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(7, e);
        Assert.Equal(AcaoNotch.AbrirClaude, e.Clicou(ParteNotch.Esquerda));
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.False(e.Pendente);
    }

    [Fact]
    public void Clique_na_direita_abre_as_notificacoes()
    {
        var e = Criar();
        Assert.Equal(AcaoNotch.AbrirNotificacoes, e.Clicou(ParteNotch.Direita));
    }

    [Fact]
    public void Pendente_do_aviso_ate_abrir()
    {
        var e = Criar();
        e.Chegou(A1);
        Assert.True(e.Pendente);
        Avancar(7, e);
        Assert.True(e.Pendente);
        e.Chegou(A2);
        e.Clicou();
        Assert.False(e.Pendente);
    }

    private static PedidoPermissao P(string id, string sessao = "vitral") => new(id, sessao, "Bash", "Claude quer rodar um comando", "npm test");

    [Fact]
    public void Pedido_abre_a_ficha()
    {
        var e = Criar();
        var p = P("1");
        e.Pediu(p);
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        Assert.Same(p, e.PedidoAtual);
    }

    [Fact]
    public void Responder_encerra_com_a_escolha_e_confirma()
    {
        var e = Criar();
        var encerrados = new List<(PedidoPermissao, EscolhaPermissao?)>();
        e.Encerrado += (p, x) => encerrados.Add((p, x));
        var pedido = P("1");
        e.Pediu(pedido);
        Avancar(0.6, e);
        Assert.Same(pedido, e.Respondeu(pedido.Id, EscolhaPermissao.Permitir));
        Assert.Equal([(pedido, (EscolhaPermissao?)EscolhaPermissao.Permitir)], encerrados);
        Assert.True(e.Confirmando);
        Assert.Equal(EscolhaPermissao.Permitir, e.UltimaEscolha);
        Avancar(1.1, e);
        Assert.False(e.Confirmando);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Sessenta_segundos_sem_resposta_encerra_com_null()
    {
        var e = Criar();
        var encerrados = new List<EscolhaPermissao?>();
        e.Encerrado += (_, x) => encerrados.Add(x);
        e.Pediu(P("1"));
        Avancar(59.9, e);
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        Avancar(0.2, e);
        Assert.Equal([(EscolhaPermissao?)null], encerrados);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
        Assert.True(e.PedidoPerdido);
        Assert.True(e.Pendente);
    }

    [Fact]
    public void Fila_com_mais_um()
    {
        var e = Criar();
        var segundo = P("2");
        e.Pediu(P("1"));
        e.Pediu(segundo);
        Assert.Equal(1, e.NaFila);
        Avancar(0.6, e);
        e.Respondeu("1", EscolhaPermissao.Negar);
        Avancar(1.1, e);
        Assert.Same(segundo, e.PedidoAtual);
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        Assert.Equal(0, e.NaFila);
    }

    [Fact]
    public void Tempo_do_segundo_conta_desde_que_ele_chegou()
    {
        var e = Criar();
        var encerrados = new List<(string, EscolhaPermissao?)>();
        e.Encerrado += (p, x) => encerrados.Add((p.Id, x));
        e.Pediu(P("1"));
        Avancar(50, e);
        e.Pediu(P("2"));
        Avancar(5, e);
        e.Respondeu("1", EscolhaPermissao.Permitir);
        Avancar(54.9, e);
        Assert.DoesNotContain(("2", (EscolhaPermissao?)null), encerrados);
        Avancar(0.2, e);
        Assert.Contains(("2", (EscolhaPermissao?)null), encerrados);
    }

    [Fact]
    public void Aviso_da_mesma_sessao_descarta_o_pedido()
    {
        var e = Criar();
        var encerrados = new List<EscolhaPermissao?>();
        e.Encerrado += (_, x) => encerrados.Add(x);
        e.Pediu(P("1", "vitral"));
        e.Chegou(new AvisoClaude(TipoAviso.Terminou, "vitral", "feito"));
        Assert.Equal([(EscolhaPermissao?)null], encerrados);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Aviso_de_outra_sessao_nao_toma_o_notch()
    {
        var e = Criar();
        e.Pediu(P("1", "vitral"));
        e.Chegou(new AvisoClaude(TipoAviso.Terminou, "sic", "feito"));
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        Assert.True(e.Pendente);
    }

    [Fact]
    public void Mouse_em_cima_nao_segura_o_pedido()
    {
        var e = Criar();
        e.Pediu(P("1"));
        e.MouseEntrou();
        Avancar(60.1, e);
        Assert.NotEqual(ModoNotch.Permissao, e.Modo);
    }

    private static readonly AvisoClaude Atualizacao = new(TipoAviso.Atualizacao, "Vitral", "0.7.3 pronto");

    [Fact]
    public void Clique_no_aviso_de_atualizacao_reinicia()
    {
        var e = Criar();
        e.Chegou(Atualizacao);
        Assert.Equal(AcaoNotch.Reiniciar, e.Clicou());
    }

    [Fact]
    public void Lembrete_de_atualizacao_reinicia_pela_lateral()
    {
        var e = Criar();
        e.Chegou(Atualizacao);
        Avancar(7, e);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
        Assert.Equal(AcaoNotch.Reiniciar, e.Clicou(ParteNotch.Esquerda));
    }

    [Fact]
    public void Aviso_do_Claude_continua_abrindo_o_Claude()
    {
        var e = Criar();
        e.Chegou(A1);
        Assert.Equal(AcaoNotch.AbrirClaude, e.Clicou());
        e.Chegou(A1);
        Avancar(7, e);
        Assert.Equal(AcaoNotch.AbrirClaude, e.Clicou(ParteNotch.Esquerda));
    }

    [Fact]
    public void Aviso_de_atualizacao_nao_descarta_pedido_de_sessao_Vitral()
    {
        var e = Criar();
        var encerrados = 0;
        e.Encerrado += (_, _) => encerrados++;
        e.Pediu(P("1", "Vitral"));
        e.Chegou(Atualizacao);
        Assert.Equal(0, encerrados);
        Assert.Equal(ModoNotch.Permissao, e.Modo);
    }

    [Fact]
    public void Pedido_perdido_com_atualizacao_pendente_abre_o_Claude()
    {
        var e = Criar();
        e.Chegou(Atualizacao);
        e.Pediu(P("1"));
        Avancar(60.1, e);
        Assert.True(e.PedidoPerdido);
        Assert.Equal(AcaoNotch.AbrirClaude, e.Clicou(ParteNotch.Esquerda));
    }

    [Fact]
    public void Clique_logo_que_o_pedido_aparece_e_ignorado()
    {
        var e = Criar();
        var encerrados = 0;
        e.Encerrado += (_, _) => encerrados++;
        e.Pediu(P("1"));
        Avancar(0.2, e);
        Assert.Null(e.Respondeu("1", EscolhaPermissao.SempreNaSessao));
        Assert.Equal(0, encerrados);
        Avancar(0.4, e);
        Assert.NotNull(e.Respondeu("1", EscolhaPermissao.SempreNaSessao));
    }

    [Fact]
    public void Clique_logo_que_o_seguinte_entra_e_ignorado()
    {
        var e = Criar();
        e.Pediu(P("1"));
        e.Pediu(P("2"));
        Avancar(0.6, e);
        e.Respondeu("1", EscolhaPermissao.Permitir);
        Avancar(1.1, e);
        Assert.Equal("2", e.PedidoAtual!.Id);
        Assert.Null(e.Respondeu("2", EscolhaPermissao.SempreNaSessao));
    }

    [Fact]
    public void Clique_em_pedido_que_ja_saiu_e_ignorado()
    {
        var e = Criar();
        e.Pediu(P("2"));
        Avancar(0.6, e);
        Assert.Null(e.Respondeu("1", EscolhaPermissao.Permitir));
        Assert.Equal(ModoNotch.Permissao, e.Modo);
    }

    [Fact]
    public void Aviso_de_outra_sessao_na_mesma_pasta_nao_descarta()
    {
        var e = Criar();
        e.Pediu(P("1") with { SessaoId = "s2" });
        e.Chegou(A1 with { SessaoId = "s1" });
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        Assert.Equal("1", e.PedidoAtual!.Id);
    }

    [Fact]
    public void Aviso_que_tira_o_atual_tambem_segura_o_clique_no_seguinte()
    {
        var e = Criar();
        e.Pediu(P("1", "vitral"));
        e.Pediu(P("2", "sic"));
        Avancar(0.6, e);
        e.Chegou(A1); // sessão vitral: tira o 1, o 2 vai para a ficha
        Assert.Equal("2", e.PedidoAtual!.Id);
        Assert.Null(e.Respondeu("2", EscolhaPermissao.SempreNaSessao));
    }

    [Fact]
    public void Pedido_que_o_Claude_largou_sai_sem_marcar_perdido()
    {
        var e = Criar();
        var encerrados = new List<EscolhaPermissao?>();
        e.Encerrado += (_, x) => encerrados.Add(x);
        e.Pediu(P("1"));
        e.Pediu(P("2", "sic"));
        e.Desistiu("1"); // respondido no app: o Claude fechou o gancho
        Assert.Equal([(EscolhaPermissao?)null], encerrados);
        Assert.Equal("2", e.PedidoAtual!.Id);
        e.Desistiu("2");
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.False(e.PedidoPerdido);
    }

    [Fact]
    public void Pedido_que_vence_durante_a_confirmacao_conta_como_sem_resposta()
    {
        var e = Criar();
        e.Pediu(P("1"));
        e.Pediu(P("2", "sic"));
        Avancar(59.5, e);
        e.Respondeu("1", EscolhaPermissao.Permitir);
        Avancar(0.6, e); // o 2 vence durante a confirmação do 1
        Avancar(1, e);
        Assert.True(e.PedidoPerdido);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
    }

    [Fact]
    public void Aviso_novo_do_Claude_limpa_o_pedido_sem_resposta()
    {
        var e = Criar();
        e.Pediu(P("1"));
        Avancar(60.1, e);
        Assert.True(e.PedidoPerdido);
        e.Chegou(A1);
        Assert.False(e.PedidoPerdido);
    }

    [Fact]
    public void Clique_no_pedido_perdido_mantem_o_lembrete_da_atualizacao()
    {
        var e = Criar();
        e.Chegou(Atualizacao);
        e.Pediu(P("1"));
        Avancar(60.1, e);
        Assert.Equal(AcaoNotch.AbrirClaude, e.Clicou(ParteNotch.Esquerda));
        Assert.True(e.Pendente);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
        Assert.Equal(AcaoNotch.Reiniciar, e.Clicou(ParteNotch.Esquerda));
    }

    [Fact]
    public void Mensageiro_espera_mais_que_o_prazo_do_notch() =>
        Assert.True(Canal.EsperaPedidoMs > EstadoNotch.TempoPedido * 1000 + 1000); // o notch decide antes de o mensageiro desistir

    [Fact]
    public void Aviso_da_mesma_sessao_pelo_id_descarta()
    {
        var e = Criar();
        e.Pediu(P("1") with { SessaoId = "s1" });
        e.Chegou(A1 with { SessaoId = "s1" });
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }
}

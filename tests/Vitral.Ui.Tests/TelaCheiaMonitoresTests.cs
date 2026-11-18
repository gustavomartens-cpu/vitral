using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class TelaCheiaMonitoresTests
{
    private static readonly Retangulo Principal = new(0, 0, 2560, 1440);
    private static readonly Retangulo Jogo = new(0, 0, 2560, 1440);
    private static readonly Retangulo NoOutroMonitor = new(2560, 0, 1920, 1080);

    [Fact]
    public void Clicar_no_outro_monitor_nao_tira_a_tela_cheia_do_principal() =>
        Assert.True(TelaCheia.NoPrincipal([("Chrome_WidgetWin_1", NoOutroMonitor), ("UnityWndClass", Jogo)], Principal));

    [Fact]
    public void Borda_invisivel_de_janela_maximizada_no_outro_monitor_nao_conta() =>
        // O Claude maximizado no monitor da esquerda (2/11): a borda invisível avança 8 px no principal.
        Assert.True(TelaCheia.NoPrincipal([("Chrome_WidgetWin_1", new Retangulo(-1088, -485, 1096, 1936)), ("UnityWndClass", Jogo)], Principal));

    [Fact]
    public void Janela_pequena_por_cima_no_principal_tira_a_tela_cheia() =>
        Assert.False(TelaCheia.NoPrincipal([("Notepad", new Retangulo(100, 100, 800, 600)), ("UnityWndClass", Jogo)], Principal));

    [Fact]
    public void Sem_janela_cobrindo_nao_e_tela_cheia() =>
        Assert.False(TelaCheia.NoPrincipal([("Chrome_WidgetWin_1", NoOutroMonitor), ("Progman", Principal)], Principal));

    [Fact]
    public void Area_de_trabalho_no_topo_nao_conta() =>
        Assert.False(TelaCheia.NoPrincipal([("WorkerW", Principal)], Principal));

    // Doca que se esconde sem a barra do topo (revisão final, I1): nada reserva espaço e a janela maximizada, com a
    // borda invisível, passa da tela. Isso não é tela cheia; um jogo maximizado sem moldura (cabe exato) continua sendo.
    [Fact]
    public void Janela_maximizada_com_borda_invisivel_nao_e_tela_cheia() =>
        Assert.False(TelaCheia.NoPrincipal([("Notepad", new Retangulo(-8, -8, 2576, 1456), true)], Principal));

    [Fact]
    public void Maximizada_que_cabe_exata_continua_tela_cheia() =>
        Assert.True(TelaCheia.NoPrincipal([("UnityWndClass", Jogo, true)], Principal));

    [Fact]
    public void Nao_maximizada_cobrindo_tudo_continua_tela_cheia() =>
        Assert.True(TelaCheia.NoPrincipal([("Chrome_WidgetWin_1", new Retangulo(-8, -8, 2576, 1456), false)], Principal));
}

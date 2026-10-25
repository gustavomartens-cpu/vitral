using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class MenuDoIconeTests
{
    private static readonly TimeSpan Pouco = TimeSpan.FromMilliseconds(300), Muito = TimeSpan.FromSeconds(2);

    [Fact]
    public void Espera_enquanto_o_menu_esta_aberto() =>
        Assert.Equal(DecisaoMenu.Esperar, MenuDoIcone.Decidir(menuVisivel: true, vistoMenu: true, Muito, outraJanelaNaFrente: false));

    [Fact]
    public void Espera_o_menu_aparecer() =>
        Assert.Equal(DecisaoMenu.Esperar, MenuDoIcone.Decidir(menuVisivel: false, vistoMenu: false, Pouco, outraJanelaNaFrente: false));

    [Fact]
    public void Menu_fechado_sem_janela_nova_reativa_o_painel() =>
        Assert.Equal(DecisaoMenu.Reativar, MenuDoIcone.Decidir(menuVisivel: false, vistoMenu: true, Pouco, outraJanelaNaFrente: false));

    [Fact]
    public void Menu_fechado_com_janela_aberta_pelo_app_fecha_o_painel() =>
        Assert.Equal(DecisaoMenu.Fechar, MenuDoIcone.Decidir(menuVisivel: false, vistoMenu: true, Pouco, outraJanelaNaFrente: true));

    [Fact]
    public void Sem_menu_depois_de_um_tempo_decide_pela_janela_da_frente()
    {
        Assert.Equal(DecisaoMenu.Fechar, MenuDoIcone.Decidir(menuVisivel: false, vistoMenu: false, Muito, outraJanelaNaFrente: true));
        Assert.Equal(DecisaoMenu.Reativar, MenuDoIcone.Decidir(menuVisivel: false, vistoMenu: false, Muito, outraJanelaNaFrente: false));
    }
}

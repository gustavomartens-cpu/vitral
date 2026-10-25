namespace Vitral.Ui.Barra;

public enum DecisaoMenu { Esperar, Reativar, Fechar }

/// Clique direito num ícone do painel: o app dono abre o menu dele e o painel perde o foco. O painel fica aberto
/// enquanto o menu está na tela; quando o menu some, volta a ser o painel ativo, a não ser que o app tenha aberto
/// uma janela (aí o painel sai da frente dela).
public static class MenuDoIcone
{
    /// Quanto esperar um menu que ainda não apareceu (apps com menu próprio, que não é o do Windows).
    public static readonly TimeSpan EsperaPeloMenu = TimeSpan.FromSeconds(1);

    public static DecisaoMenu Decidir(bool menuVisivel, bool vistoMenu, TimeSpan decorrido, bool outraJanelaNaFrente)
    {
        if (menuVisivel) return DecisaoMenu.Esperar;
        if (!vistoMenu && decorrido < EsperaPeloMenu) return DecisaoMenu.Esperar;
        return outraJanelaNaFrente ? DecisaoMenu.Fechar : DecisaoMenu.Reativar;
    }
}

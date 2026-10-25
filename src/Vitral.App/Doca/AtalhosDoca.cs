using System.Diagnostics;
using Vitral.App.Barra;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

/// Ações da doca que saem para o Windows: o menu Iniciar e a pesquisa pelas teclas, os apps pelo shell.
internal sealed class AtalhosDoca : IAtalhosDoca
{
    public void Iniciar() => Atalhos.Win();

    public void Pesquisar() => Atalhos.Pesquisa();

    public void Abrir(Fixado fixado) => Executar(fixado.Caminho, "AtalhosDoca.Abrir");

    public void NovaJanela(string executavel) => Executar(executavel, "AtalhosDoca.NovaJanela");

    private static void Executar(string caminho, string onde)
    {
        try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
        catch (Exception e) { LogBarra.Erro(onde, e); }
    }
}

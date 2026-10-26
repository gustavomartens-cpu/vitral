using Velopack;

namespace Vitral.App;

/// Ponto de entrada. O Velopack precisa rodar antes de tudo (ele trata a instalação, a atualização e a desinstalação).
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Gancho do Claude Code (spec do notch vivo §4.3): entrega o aviso e sai, sem Velopack nem interface.
        // Prazo total de 1,5 s: o Claude nunca espera mais que isso, aconteça o que acontecer (as threads do pool morrem
        // com o processo).
        if (args is ["--aviso", ..]) { Task.Run(Claude.Mensageiro.Executar).Wait(1500); return; }

        // Desinstalar com a doca ligada não pode deixar o usuário sem barra de tarefas.
        VelopackApp.Build().OnBeforeUninstallFastCallback(_ => { Doca.SaidaDaDoca.AoDesinstalar(); Claude.ServicoGanchosClaude.DesligarAoDesinstalar(); }).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

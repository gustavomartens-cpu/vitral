using Velopack;

namespace Vitral.App;

/// Ponto de entrada. O Velopack precisa rodar antes de tudo (ele trata a instalação, a atualização e a desinstalação).
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Gancho do Claude Code (spec do notch vivo §4.3): entrega o aviso e sai, sem Velopack nem interface.
        // Prazo total: 1,5 s nos avisos, 65 s no pedido de permissão e 19,5 min no Stop que espera a resposta pelo notch
        // (specs das permissões §3.4 e da resposta §3.3). Aconteça o que acontecer, o Claude não espera mais que isso (as
        // threads do pool morrem com o processo). A janela da frente é lida uma vez só, para o prazo e a decisão.
        if (args is ["--aviso", ..])
        {
            var entrada = Claude.Mensageiro.LerEntrada();
            var naFrente = Claude.Mensageiro.ClaudeNaFrente();
            Task.Run(() => Claude.Mensageiro.Executar(entrada, naFrente)).Wait(Claude.Mensageiro.PrazoMs(entrada, naFrente));
            return;
        }

        // Desinstalar com a doca ligada não pode deixar o usuário sem barra de tarefas.
        VelopackApp.Build().OnBeforeUninstallFastCallback(_ => { Doca.SaidaDaDoca.AoDesinstalar(); Claude.ServicoGanchosClaude.DesligarAoDesinstalar(); }).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

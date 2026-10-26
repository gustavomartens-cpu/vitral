using Velopack;

namespace Vitral.App;

/// Ponto de entrada. O Velopack precisa rodar antes de tudo (ele trata a instalação, a atualização e a desinstalação).
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Gancho do Claude Code (spec do notch vivo §4.3): entrega o aviso e sai, sem Velopack nem interface.
        // Prazo total: 1,5 s nos avisos e 65 s no pedido de permissão, que espera a resposta do notch (spec das permissões
        // §3.4). Aconteça o que acontecer, o Claude não espera mais que isso (as threads do pool morrem com o processo).
        if (args is ["--aviso", ..])
        {
            var entrada = Claude.Mensageiro.LerEntrada();
            Task.Run(() => Claude.Mensageiro.Executar(entrada)).Wait(Claude.Mensageiro.EhPermissao(entrada) ? 65_000 : 1_500);
            return;
        }

        // Desinstalar com a doca ligada não pode deixar o usuário sem barra de tarefas.
        VelopackApp.Build().OnBeforeUninstallFastCallback(_ => { Doca.SaidaDaDoca.AoDesinstalar(); Claude.ServicoGanchosClaude.DesligarAoDesinstalar(); }).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

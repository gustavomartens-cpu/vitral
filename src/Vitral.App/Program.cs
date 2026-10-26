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
        // threads do pool morrem com o processo). A decisão de segurar é tomada uma vez só, para o prazo e para a ficha.
        if (args is ["--aviso", ..])
        {
            var entrada = Claude.Mensageiro.LerEntrada();
            var segurar = Claude.Mensageiro.SegurarStop(entrada);
            Task.Run(() => Claude.Mensageiro.Executar(entrada, segurar)).Wait(Claude.Mensageiro.PrazoMs(entrada, segurar));
            return;
        }

        // Desinstalar com a doca ligada não pode deixar o usuário sem barra de tarefas.
        VelopackApp.Build().OnBeforeUninstallFastCallback(_ => { Doca.SaidaDaDoca.AoDesinstalar(); Claude.ServicoGanchosClaude.DesligarAoDesinstalar(); }).Run();
        // O Velopack abre o Vitral com a pasta de trabalho na pasta da instalação, e todo programa aberto pela doca a
        // herda. Um app que fica aberto (o do Claude, por exemplo) prendia essa pasta e a atualização não conseguia
        // trocá-la. A partir daqui o Vitral trabalha na pasta do usuário.
        Environment.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

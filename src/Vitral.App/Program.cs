using Velopack;

namespace Vitral.App;

/// Ponto de entrada. O Velopack precisa rodar antes de tudo (ele trata a instalação, a atualização e a desinstalação).
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Gancho do Claude Code (spec do notch vivo §4.3): entrega o aviso e sai, sem Velopack nem interface.
        if (args is ["--aviso", ..]) { Claude.Mensageiro.Executar(); return; }

        // Desinstalar com a doca ligada não pode deixar o usuário sem barra de tarefas.
        VelopackApp.Build().OnBeforeUninstallFastCallback(_ => Doca.SaidaDaDoca.AoDesinstalar()).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

using Velopack;

namespace Vitral.App;

/// Ponto de entrada. O Velopack precisa rodar antes de tudo (ele trata a instalação, a atualização e a desinstalação).
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

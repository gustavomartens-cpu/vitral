using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Vitral.Ui;

namespace Vitral.App;

/// Ícone do Vitral perto do relógio. O menu é refeito a cada abertura, com os perfis atuais.
internal sealed class Bandeja : IDisposable
{
    private readonly TaskbarIcon _icone;
    private readonly App _app;

    public Bandeja(App app)
    {
        _app = app;
        _icone = new TaskbarIcon
        {
            ToolTipText = "Vitral",
            IconSource = BitmapFrame.Create(new Uri("pack://application:,,,/Recursos/vitral.ico")),
            ContextMenu = new ContextMenu(),
        };
        _icone.ContextMenu.Opened += (_, _) => MontarMenu();
        _icone.TrayLeftMouseUp += (_, _) => _app.MostrarJanela();
        _icone.ForceCreate(false);
    }

    public void Avisar(string titulo, string mensagem) => _icone.ShowNotification(titulo, mensagem);

    private void MontarMenu()
    {
        var menu = _icone.ContextMenu!;
        menu.Items.Clear();
        var vm = _app.Principal;
        foreach (var item in MenuBandeja.Montar(vm.NomesParaMenu(), vm.PerfilAtivo)) // não mexe na janela (editor não salvo)
        {
            if (item.Separador) { menu.Items.Add(new Separator()); continue; }
            var mi = new MenuItem { Header = item.Texto, IsCheckable = false, IsChecked = item.Marcado };
            string acao = item.Acao;
            mi.Click += (_, _) => Executar(acao);
            menu.Items.Add(mi);
        }
    }

    private void Executar(string acao)
    {
        var vm = _app.Principal;
        switch (acao)
        {
            case "desfazer": vm.DesfazerCommand.Execute(null); Mostrar(vm.Mensagem); break;
            case "original": _app.MostrarJanela(); vm.OriginalCommand.Execute(null); break;
            case "abrir": _app.MostrarJanela(); break;
            case "sair": _app.Sair(); break;
            default:
                if (acao.StartsWith("perfil:", StringComparison.Ordinal))
                    Mostrar(vm.TrocarPelaBandeja(acao["perfil:".Length..])); // parcial ou teste já em curso
                break;
        }
    }

    private void Mostrar(string? mensagem)
    {
        if (!string.IsNullOrEmpty(mensagem)) Avisar("Vitral", mensagem);
    }

    public void Dispose() => _icone.Dispose();
}

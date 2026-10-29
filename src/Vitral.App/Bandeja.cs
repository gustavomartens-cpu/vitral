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
        _icone.ContextMenu.Opened += (_, _) => { MontarMenu(); Assentar(); };
        _icone.TrayLeftMouseUp += (_, _) => _app.MostrarJanela();
        _icone.ForceCreate(false);
    }

    /// O balão do Windows; se ele falhar (o H.NotifyIcon lança "Show notification failed"), fica só no log, sem
    /// interromper quem avisou (a doca recriando, por exemplo).
    public void Avisar(string titulo, string mensagem)
    {
        try { _icone.ShowNotification(titulo, mensagem); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Bandeja.Avisar", e); }
    }

    private void MontarMenu()
    {
        var menu = _icone.ContextMenu!;
        menu.Items.Clear();
        var vm = _app.Principal;
        foreach (var item in MenuBandeja.Montar(vm.NomesParaMenu(), vm.PerfilAtivo)) // não mexe na janela (editor não salvo)
        {
            if (item.Separador) { menu.Items.Add(new Separator()); continue; }
            var mi = new MenuItem { Header = item.Texto, IsCheckable = false, IsChecked = item.Marcado, Icon = item.Icone };
            string acao = item.Acao;
            mi.Click += (_, _) => Executar(acao);
            menu.Items.Add(mi);
        }
    }

    /// O ícone fica no topo com a doca ligada e embaixo, na barra do Windows, com ela desligada: o menu desce na metade
    /// de cima do monitor e sobe na de baixo.
    private void Assentar()
    {
        var cursor = System.Windows.Forms.Control.MousePosition;
        var tela = System.Windows.Forms.Screen.FromPoint(cursor).Bounds;
        var de = cursor.Y < tela.Top + tela.Height / 2 ? Tema.AssentarDe.Cima : Tema.AssentarDe.Baixo;
        Tema.Efeitos.Assentar(_icone.ContextMenu!, de, cascata: false);
    }

    private void Executar(string acao)
    {
        var vm = _app.Principal;
        switch (acao)
        {
            case "desfazer": vm.DesfazerCommand.Execute(null); Mostrar(vm.Mensagem); break;
            case "original": _app.MostrarJanela(); vm.OriginalCommand.Execute(null); break;
            case "abrir": _app.MostrarJanela(); break;
            case "sair": if (_app.Principal.PodeSair()) _app.Sair(); break; // alteração pendente pergunta antes
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

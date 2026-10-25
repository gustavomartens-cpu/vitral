using System.ComponentModel;
using System.Windows;
using Vitral.Core.Configuracao;
using Vitral.Ui;

namespace Vitral.App;

public partial class JanelaPrincipal : Window
{
    private readonly Inicializacao _inicializacao;
    private bool _carregando = true;

    public JanelaPrincipal(PrincipalViewModel vm, Inicializacao inicializacao)
    {
        InitializeComponent();
        DataContext = vm;
        _inicializacao = inicializacao;
        IniciarComWindows.IsChecked = inicializacao.Ativa();
        TextoBuild.Text = $"Windows {Environment.OSVersion.Version}";
        _carregando = false;
    }

    private void IniciarComWindows_Mudou(object sender, RoutedEventArgs e)
    {
        if (_carregando) return;
        if (IniciarComWindows.IsChecked == true) _inicializacao.Ativar(Environment.ProcessPath!);
        else _inicializacao.Desativar();
    }

    /// Fechar a janela só esconde: o Vitral continua na bandeja. "Sair" fica no menu da bandeja.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

using System.ComponentModel;
using System.Windows;
using Vitral.Core.Configuracao;
using Vitral.Ui;

namespace Vitral.App;

public partial class JanelaPrincipal : Window
{
    private readonly Inicializacao _inicializacao;
    private readonly Claude.ServicoGanchosClaude _ganchos = new();
    private bool _carregando = true;

    public JanelaPrincipal(PrincipalViewModel vm, Inicializacao inicializacao)
    {
        InitializeComponent();
        Vidro.Aplicar(this);
        DataContext = vm;
        _inicializacao = inicializacao;
        IniciarComWindows.IsChecked = inicializacao.Ativa();
        AvisosClaude.IsChecked = _ganchos.EstaLigado();
        TextoBuild.Text = $"Windows {Environment.OSVersion.Version}";
        _carregando = false;
    }

    private void IniciarComWindows_Mudou(object sender, RoutedEventArgs e)
    {
        if (_carregando) return;
        try
        {
            if (IniciarComWindows.IsChecked == true) _inicializacao.Ativar(Environment.ProcessPath!);
            else _inicializacao.Desativar();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MessageBox.Show($"Não foi possível mudar o início com o Windows: {ex.Message}", "Vitral");
            _carregando = true;
            IniciarComWindows.IsChecked = _inicializacao.Ativa();
            _carregando = false;
        }
    }

    /// Liga ou desliga os ganchos do Claude Code; em falha, a caixa volta ao que está no arquivo.
    private void AvisosClaude_Mudou(object sender, RoutedEventArgs e)
    {
        if (_carregando) return;
        try
        {
            if (AvisosClaude.IsChecked == true) _ganchos.Ligar(Environment.ProcessPath!);
            else _ganchos.Desligar();
        }
        catch (FormatException)
        {
            MessageBox.Show("O settings.json do Claude está com defeito; corrija-o antes.", "Vitral");
            VoltarAvisosClaude();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MessageBox.Show($"Não foi possível alterar os avisos do Claude: {ex.Message}", "Vitral");
            VoltarAvisosClaude();
        }
    }

    private void VoltarAvisosClaude()
    {
        _carregando = true;
        AvisosClaude.IsChecked = _ganchos.EstaLigado();
        _carregando = false;
    }

    /// Fechar a janela só esconde: o Vitral continua na bandeja. "Sair" fica no menu da bandeja.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

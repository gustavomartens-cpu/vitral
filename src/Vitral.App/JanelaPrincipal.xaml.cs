using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Vitral.Core.Atualizacao;
using Vitral.Core.Configuracao;
using Vitral.Ui;

namespace Vitral.App;

public partial class JanelaPrincipal : Window
{
    private readonly Inicializacao _inicializacao;
    private readonly Claude.ServicoGanchosClaude _ganchos = new();
    private readonly ServicoAtualizacao _atualizacao;
    private bool _carregando = true;

    public JanelaPrincipal(PrincipalViewModel vm, Inicializacao inicializacao, ServicoAtualizacao atualizacao, string versao)
    {
        InitializeComponent();
        Vidro.Aplicar(this);
        DataContext = vm;
        _inicializacao = inicializacao;
        _atualizacao = atualizacao;
        IniciarComWindows.IsChecked = inicializacao.Ativa();
        AvisosClaude.IsChecked = _ganchos.EstaLigado();
        TextoBuild.Text = $"Windows {Environment.OSVersion.Version}";
        TextoVersao.Text = $"Vitral {versao}";
        _atualizacao.Mudou += MostrarAtualizacao;
        MostrarAtualizacao();
        _carregando = false;
#if DEBUG
        // F9 simula o aviso de versão pronta no notch (sem a fonte real, reiniciar não faz nada).
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.F9) ((App)Application.Current).AvisarAtualizacao("9.9.9"); };
#endif
    }

    /// A linha de estado da atualização, com "reiniciar" (versão pronta) ou "Procurar agora" clicáveis.
    private void MostrarAtualizacao()
    {
        var linha = LinhaAtualizacao.Inlines;
        linha.Clear();
        switch (_atualizacao.Estado)
        {
            case EstadoAtualizacao.Pronta:
                linha.Add(new Run($"Versão {_atualizacao.Versao} pronta — "));
                linha.Add(Link("reiniciar", _atualizacao.Reiniciar));
                break;
            case EstadoAtualizacao.Desligada or EstadoAtualizacao.Procurando or EstadoAtualizacao.Baixando:
                linha.Add(new Run(TextoAtualizacao.Para(_atualizacao.Estado, _atualizacao.Versao)));
                break;
            default:
                linha.Add(new Run(TextoAtualizacao.Para(_atualizacao.Estado, _atualizacao.Versao) + " · "));
                linha.Add(Link("Procurar agora", () => _ = _atualizacao.ProcurarAgoraAsync()));
                break;
        }
    }

    private static Hyperlink Link(string texto, Action acao)
    {
        var link = new Hyperlink(new Run(texto)) { Foreground = (Brush)Application.Current.Resources["Ambar"], TextDecorations = null, Cursor = System.Windows.Input.Cursors.Hand };
        link.Click += (_, _) => acao();
        return link;
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

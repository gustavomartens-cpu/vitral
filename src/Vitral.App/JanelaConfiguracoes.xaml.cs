using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Vitral.Core.Atualizacao;
using Vitral.Core.Configuracao;
using Vitral.Ui;

namespace Vitral.App;

/// As configurações do Vitral que são do computador (spec da interface §3.5): início com o Windows, avisos do Claude e
/// da agenda no notch, versão e atualização. Saíram da barra lateral da janela principal, com o mesmo comportamento.
public partial class JanelaConfiguracoes : Window
{
    private readonly Inicializacao _inicializacao;
    private readonly Claude.ServicoGanchosClaude _ganchos = new();
    private readonly ServicoAtualizacao _atualizacao;
    private readonly Agenda.ServicoAgendaApp? _agenda;
    private bool _carregando = true;

    internal JanelaConfiguracoes(Inicializacao inicializacao, ServicoAtualizacao atualizacao, string versao, Agenda.ServicoAgendaApp? agenda)
    {
        InitializeComponent();
        Vidro.Aplicar(this);
        _inicializacao = inicializacao;
        _atualizacao = atualizacao;
        _agenda = agenda;
        IniciarComWindows.IsChecked = inicializacao.Ativa();
        AvisosClaude.IsChecked = _ganchos.EstaLigado();
        AgendaNoNotch.IsChecked = agenda?.Config.Avisar ?? false;
        AgendaNoNotch.IsEnabled = agenda is not null;
        TextoVersao.Text = $"Vitral {versao}";
        TextoBuild.Text = $"Windows {Environment.OSVersion.Version}";
        _atualizacao.Mudou += MostrarAtualizacao;
        Closed += (_, _) => _atualizacao.Mudou -= MostrarAtualizacao;
        MostrarAtualizacao();
        _carregando = false;
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

    /// Liga ou desliga os ganchos do Claude Code; em falha, a chave volta ao que está no arquivo.
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

    private void AgendaNoNotch_Mudou(object sender, RoutedEventArgs e)
    {
        if (_carregando || _agenda is null) return;
        _agenda.DefinirAvisar(AgendaNoNotch.IsChecked == true);
    }

    private void ContasAgenda_Click(object sender, RoutedEventArgs e)
    {
        if (_agenda is null) return;
        new Agenda.JanelaContas(_agenda) { Owner = this }.ShowDialog();
    }
}

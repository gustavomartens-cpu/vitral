using System.Windows;
using Vitral.Core.Agenda;
using Vitral.Ui;

namespace Vitral.App.Agenda;

/// Uma conta na lista da janela.
internal sealed record LinhaConta(ContaAgenda Conta, string Email, string Provedor, string Estado, bool PrecisaEntrar, string Cor);

/// Ligar e remover as contas da agenda e escolher a antecedência do aviso (spec da agenda §5.3).
public partial class JanelaContas : Window
{
    private static readonly IReadOnlyList<Opcao<int>> Antecedencias = [new("5 min", 5), new("10 min", 10), new("15 min", 15)];
    private readonly ServicoAgendaApp _agenda;
    private bool _carregando;

    internal JanelaContas(ServicoAgendaApp agenda)
    {
        InitializeComponent();
        Vidro.Aplicar(this);
        _agenda = agenda;
        EntrarGoogle.IsEnabled = ServicoAgendaApp.Disponivel(ProvedorAgenda.Google);
        EntrarMicrosoft.IsEnabled = ServicoAgendaApp.Disponivel(ProvedorAgenda.Microsoft);
        if (!EntrarGoogle.IsEnabled) EntrarGoogle.ToolTip = "Falta cadastrar o app no Google (docs/agenda-cadastro.md)";
        if (!EntrarMicrosoft.IsEnabled) EntrarMicrosoft.ToolTip = "Falta cadastrar o app na Microsoft (docs/agenda-cadastro.md)";
        _carregando = true;
        Antecedencia.ItemsSource = Antecedencias;
        Antecedencia.SelectedValue = _agenda.Config.AntecedenciaMin;
        _carregando = false;
        _agenda.Agenda.Mudou += Recarregar;
        Closed += (_, _) => _agenda.Agenda.Mudou -= Recarregar;
        Recarregar();
    }

    private void Recarregar() => Dispatcher.BeginInvoke(() =>
    {
        var estados = _agenda.Agenda.Contas;
        var linhas = _agenda.Config.Contas.Select(c =>
        {
            var (estado, quando) = estados.TryGetValue(c, out var v) ? v : (EstadoConta.Ok, null);
            string texto = estado switch
            {
                EstadoConta.PrecisaEntrar => "precisa entrar de novo",
                EstadoConta.SemConexao => quando is { } q ? $"atualizado às {q.ToLocalTime():HH:mm}" : "sem conexão",
                _ => quando is null ? "lendo…" : "ok",
            };
            return new LinhaConta(c, c.Email, c.Provedor == ProvedorAgenda.Google ? "Google" : "Microsoft", texto,
                estado == EstadoConta.PrecisaEntrar, c.Cor);
        }).ToList();
        Lista.ItemsSource = linhas;
        SemContas.Visibility = linhas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    });

    private async Task Entrar(ProvedorAgenda provedor)
    {
        IsEnabled = false;
        Mostrar("Continue no navegador que abriu…");
        try { Mostrar(await _agenda.Entrar(provedor)); }
        finally { IsEnabled = true; Recarregar(); }
    }

    private void Mostrar(string? texto)
    {
        Mensagem.Text = texto ?? "";
        Mensagem.Visibility = string.IsNullOrEmpty(texto) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void EntrarGoogle_Click(object sender, RoutedEventArgs e) => await Entrar(ProvedorAgenda.Google);

    private async void EntrarMicrosoft_Click(object sender, RoutedEventArgs e) => await Entrar(ProvedorAgenda.Microsoft);

    private async void EntrarDeNovo_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is LinhaConta linha) await Entrar(linha.Conta.Provedor);
    }

    private async void Remover_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not LinhaConta linha) return;
        if (MessageBox.Show(this, $"Remover a conta {linha.Email}? O Vitral deixa de ler essa agenda e apaga o acesso deste computador.",
                "Contas da agenda", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await _agenda.Remover(linha.Conta);
        Recarregar();
    }

    private void Antecedencia_Mudou(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_carregando && Antecedencia.SelectedValue is int minutos) _agenda.DefinirAntecedencia(minutos);
    }
}

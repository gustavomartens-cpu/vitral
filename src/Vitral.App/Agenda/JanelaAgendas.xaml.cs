using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Vitral.Core.Agenda;

namespace Vitral.App.Agenda;

/// Uma agenda na lista, com a caixa de marcar. A principal vem marcada e travada.
internal sealed partial class LinhaAgenda(AgendaGoogle agenda, bool marcada) : ObservableObject
{
    public AgendaGoogle Agenda { get; } = agenda;
    public string Nome => Agenda.Principal ? $"{Agenda.Nome} (principal)" : Agenda.Nome;
    public bool PodeMudar => !Agenda.Principal;
    [ObservableProperty] private bool _marcada = marcada || agenda.Principal;
}

/// Escolher as agendas da conta do Google que o Vitral lê além da principal.
public partial class JanelaAgendas : Window
{
    private readonly ServicoAgendaApp _agenda;
    private readonly ContaAgenda _conta;
    private List<LinhaAgenda> _linhas = [];

    internal JanelaAgendas(ServicoAgendaApp agenda, ContaAgenda conta)
    {
        InitializeComponent();
        Vidro.Aplicar(this);
        _agenda = agenda;
        _conta = conta;
        Titulo.Text = conta.Email;
        Loaded += async (_, _) => await Carregar();
    }

    private async Task Carregar()
    {
        try
        {
            var escolhidas = _agenda.Config.AgendasDe(_conta).ToHashSet(StringComparer.Ordinal);
            _linhas = (await _agenda.AgendasDoGoogle(_conta)).Select(a => new LinhaAgenda(a, escolhidas.Contains(a.Id))).ToList();
            Lista.ItemsSource = _linhas;
            Estado.Visibility = Visibility.Collapsed;
            Salvar.IsEnabled = true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Barra.LogBarra.Erro("JanelaAgendas.Carregar", e);
            Estado.Text = e is ContaPrecisaEntrarException ? "A conta precisa entrar de novo." : "Não foi possível ler as agendas do Google. Tente de novo em alguns minutos.";
        }
    }

    private async void Salvar_Click(object sender, RoutedEventArgs e)
    {
        Salvar.IsEnabled = false;
        await _agenda.DefinirAgendas(_conta, _linhas.Where(l => l.Marcada && !l.Agenda.Principal).Select(l => l.Agenda.Id));
        DialogResult = true;
    }
}

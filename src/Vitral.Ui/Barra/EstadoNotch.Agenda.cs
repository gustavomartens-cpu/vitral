using Vitral.Core.Agenda;

namespace Vitral.Ui.Barra;

/// Um compromisso para avisar no notch, com a antecedência em vigor (para o "em 10 min").
public sealed record AvisoAgenda(Compromisso Compromisso, TimeSpan Antecedencia);

/// Os avisos da agenda no notch (spec da agenda §5.5): esperam as fichas e o aviso que estiver na tela, ficam 6 s e viram
/// lembrete até o clique, o Entrar ou o fim do compromisso.
public sealed partial class EstadoNotch
{
    private readonly List<AvisoAgenda> _agenda = [];

    /// O compromisso no aviso ou no lembrete (null sem nenhum).
    public AvisoAgenda? AvisoAgendaAtual { get; private set; }

    /// O aviso na tela é o da agenda (e não o do Claude).
    public bool AvisoEhDaAgenda { get; private set; }

    /// O dia do compromisso cujo aviso foi clicado (o calendário abre nele); null nos outros cliques.
    public DateOnly? DiaPedido { get; private set; }

    private bool FichaOcupada => _fila.Count > 0 || Confirmando || _respostas.Count > 0 || ConfirmandoResposta;

    public void Lembrou(AvisoAgenda aviso)
    {
        _agenda.Add(aviso);
        if (FichaOcupada || Modo == ModoNotch.Aviso) { Mudou?.Invoke(); return; } // espera a vez
        MostrarAgenda();
    }

    /// O compromisso foi cancelado ou saiu da agenda: sai da fila e do lembrete.
    public void Sumiu(string idCompromisso)
    {
        _agenda.RemoveAll(a => a.Compromisso.Id == idCompromisso);
        if (AvisoAgendaAtual?.Compromisso.Id == idCompromisso) LimparAgenda();
        else Mudou?.Invoke();
    }

    /// O clique no "Entrar": devolve o link da reunião e encerra o aviso.
    public Uri? EntrarNaReuniao()
    {
        var link = AvisoAgendaAtual?.Compromisso.LinkReuniao;
        LimparAgenda();
        return link;
    }

    /// O próximo da fila vai para o aviso; false se a fila está vazia.
    private bool MostrarAgenda()
    {
        if (_agenda.Count == 0) return false;
        AvisoAgendaAtual = _agenda[0];
        _agenda.RemoveAt(0);
        AvisoEhDaAgenda = true;
        Laterais = false;
        _inicio = agora();
        Ir(ModoNotch.Aviso, forcar: true);
        return true;
    }

    private void LimparAgenda()
    {
        AvisoAgendaAtual = null;
        bool estavaNaTela = AvisoEhDaAgenda && Modo == ModoNotch.Aviso;
        AvisoEhDaAgenda = false;
        if (estavaNaTela || Modo == ModoNotch.Lembrete)
        {
            if (!MostrarAgenda()) Ir(Pendente ? ModoNotch.Lembrete : ModoNotch.Normal, forcar: true);
        }
        else Mudou?.Invoke();
    }

    /// O lembrete da agenda fica até o fim do compromisso.
    private void TiqueAgenda(DateTime t)
    {
        if (AvisoAgendaAtual is { } a && new DateTimeOffset(t) >= a.Compromisso.Fim) LimparAgenda();
    }

    /// O clique que abre o calendário leva ao dia do compromisso do aviso (ou do lembrete) e o encerra.
    private void CliqueNaAgenda(AcaoNotch acao)
    {
        DiaPedido = null;
        if (acao != AcaoNotch.AbrirCalendario || AvisoAgendaAtual is not { } a) return;
        DiaPedido = DateOnly.FromDateTime(a.Compromisso.Inicio.LocalDateTime);
        AvisoAgendaAtual = null;
        AvisoEhDaAgenda = false;
    }
}

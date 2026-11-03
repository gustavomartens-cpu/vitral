using Vitral.Core.Notificacoes;

namespace Vitral.Ui.Barra;

/// Uma notificação de outro app para avisar no notch (spec das notificações §5); <see cref="Quantas"/> > 1 é a rajada.
public sealed record AvisoNotificacao(string AppId, string NomeApp, string? Titulo, string? Texto, int Quantas = 1)
{
    /// Até 3 novas de uma vez descem uma a uma; mais que isso viram um aviso só ("{n} notificações novas").
    public static IReadOnlyList<AvisoNotificacao> De(IReadOnlyList<NotificacaoApp> novas) =>
        novas.Count > 3
            ? [new(novas[0].AppId, novas[0].NomeApp, $"{novas.Count} notificações novas", null, novas.Count)]
            : novas.Select(n => new AvisoNotificacao(n.AppId, n.NomeApp, n.Titulo, n.Texto)).ToList();
}

/// Os avisos das notificações no notch (spec das notificações §5): esperam as fichas e o aviso que estiver na tela, ficam
/// 6 s e saem (não viram lembrete: ficam no painel do sino).
public sealed partial class EstadoNotch
{
    private readonly List<AvisoNotificacao> _notificacoes = [];
    private bool _notificacoesLigadas;
    private int _contador;

    /// A notificação no aviso (null sem nenhuma).
    public AvisoNotificacao? AvisoNotificacaoAtual { get; private set; }

    /// O app da notificação clicada (o App o abre); null nos outros cliques.
    public string? AppPedido { get; private set; }

    /// O Vitral lê as notificações: o sino abre o painel dele (senão, a central do Windows, como antes).
    public bool NotificacoesLigadas
    {
        get => _notificacoesLigadas;
        set { if (_notificacoesLigadas == value) return; _notificacoesLigadas = value; Mudou?.Invoke(); }
    }

    /// Quantas notificações há (o número do sino).
    public int Contador
    {
        get => _contador;
        set { if (_contador == value) return; _contador = value; Mudou?.Invoke(); }
    }

    /// "Não perturbe" ligado pelo painel de controles: as notificações dos apps não descem (ficam no painel do sino); os
    /// avisos do Claude e da agenda continuam (spec dos controles §4.3).
    public bool NaoPerturbe { get; set; }

    public void Notificou(AvisoNotificacao aviso)
    {
        if (NaoPerturbe) return;
        _notificacoes.Add(aviso);
        if (FichaOcupada || Modo == ModoNotch.Aviso) { Mudou?.Invoke(); return; } // espera a vez
        MostrarNotificacao();
    }

    /// A próxima notificação da fila vai para o aviso; false se a fila está vazia.
    private bool MostrarNotificacao()
    {
        if (_notificacoes.Count == 0) return false;
        AvisoNotificacaoAtual = _notificacoes[0];
        _notificacoes.RemoveAt(0);
        AvisoEhDaAgenda = false;
        Laterais = false;
        _inicio = agora();
        Ir(ModoNotch.Aviso, forcar: true);
        return true;
    }

    /// Uma ficha ou um aviso da agenda tomou a tela: a notificação que estava nela volta para o começo da fila (senão
    /// ficava presa, e o próximo aviso desenharia a velha).
    private void DevolverNotificacaoAFila()
    {
        if (AvisoNotificacaoAtual is not { } n) return;
        _notificacoes.Insert(0, n);
        AvisoNotificacaoAtual = null;
    }

    /// A notificação saiu do aviso (6 s ou clique): a próxima da fila, um aviso da agenda esperando, ou o fim.
    private void EncerrarNotificacao()
    {
        AvisoNotificacaoAtual = null;
        if (MostrarNotificacao() || MostrarAgenda()) return;
        Ir(Pendente || AvisoAgendaAtual is not null ? ModoNotch.Lembrete : ModoNotch.Normal, forcar: true);
    }
}

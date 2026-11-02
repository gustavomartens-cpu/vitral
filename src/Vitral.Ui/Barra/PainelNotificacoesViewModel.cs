using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vitral.Core.Notificacoes;

namespace Vitral.Ui.Barra;

/// Uma linha do painel: a notificação com o "há quanto tempo" já escrito.
public sealed record ItemPainelNotificacao(uint Id, string AppId, string? Titulo, string? Texto, string Quando);

/// Um app no painel, com as notificações dele (a mais recente primeiro).
public sealed record GrupoPainelNotificacao(string AppId, string NomeApp, IReadOnlyList<ItemPainelNotificacao> Itens);

/// O painel que desce do sino do notch (spec das notificações §6): agrupado por app, com limpar (um, o app, tudo), abrir
/// o app e a central do Windows. Quem fala com o Windows é o App, pelos eventos.
public sealed partial class PainelNotificacoesViewModel(Func<DateTimeOffset> agora) : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<GrupoPainelNotificacao> _grupos = [];
    [ObservableProperty] private string? _textoEstado;
    [ObservableProperty] private bool _vazio;

    public event Action<uint>? PediuLimpar;
    public event Action<string>? PediuLimparApp;
    public event Action? PediuLimparTudo;
    public event Action<string>? PediuAbrirApp;
    public event Action? PediuCentral;

    public void Atualizar(EstadoNotificacoes estado, IReadOnlyList<NotificacaoApp> notificacoes)
    {
        var t = agora();
        var grupos = estado == EstadoNotificacoes.Ligado
            ? RegrasNotificacoes.Agrupar(notificacoes).Select(g => new GrupoPainelNotificacao(g.AppId, g.NomeApp,
                g.Itens.Select(n => new ItemPainelNotificacao(n.Id, n.AppId, n.Titulo, n.Texto, RegrasNotificacoes.HaQuanto(n.Chegou, t))).ToList())).ToList()
            : [];
        // Estado e vazio antes dos grupos: o painel se refaz no aviso de Grupos e lê os dois.
        TextoEstado = RegrasNotificacoes.Texto(estado);
        Vazio = estado == EstadoNotificacoes.Ligado && grupos.Count == 0;
        Grupos = grupos;
    }

    [RelayCommand] private void Limpar(ItemPainelNotificacao item) => PediuLimpar?.Invoke(item.Id);
    [RelayCommand] private void LimparApp(GrupoPainelNotificacao grupo) => PediuLimparApp?.Invoke(grupo.AppId);
    [RelayCommand] private void LimparTudo() => PediuLimparTudo?.Invoke();
    [RelayCommand] private void AbrirApp(ItemPainelNotificacao item) => PediuAbrirApp?.Invoke(item.AppId);
    [RelayCommand] private void AbrirCentral() => PediuCentral?.Invoke();
}

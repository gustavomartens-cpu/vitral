using System.Globalization;

namespace Vitral.Core.Notificacoes;

/// As regras das notificações no notch que não dependem do Windows (spec das notificações §3, §5 e §6).
public static class RegrasNotificacoes
{
    /// As do app do Claude ficam de fora: os avisos do Claude vêm pelos ganchos, que sabem a sessão.
    public static IReadOnlyList<NotificacaoApp> Filtrar(IEnumerable<NotificacaoApp> todas) =>
        todas.Where(n => !n.AppId.StartsWith("Claude_", StringComparison.OrdinalIgnoreCase)).ToList();

    /// Por app, o grupo com a notificação mais recente em cima; dentro dele, da mais recente para a mais antiga.
    public static IReadOnlyList<GrupoNotificacoes> Agrupar(IEnumerable<NotificacaoApp> todas) =>
        todas.GroupBy(n => n.AppId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var itens = g.OrderByDescending(n => n.Chegou).ToList();
                string nome = itens.Select(n => n.NomeApp).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? g.Key;
                return new GrupoNotificacoes(g.Key, nome, itens);
            })
            .OrderByDescending(g => g.Itens[0].Chegou)
            .ToList();

    /// O que chegou e o que sumiu (limpo no Windows) entre duas leituras.
    public static (IReadOnlyList<NotificacaoApp> Novas, IReadOnlyList<uint> Sumidas) Diferenca(
        IReadOnlyCollection<NotificacaoApp> antes, IReadOnlyCollection<NotificacaoApp> agora)
    {
        var idsAntes = antes.Select(n => n.Id).ToHashSet();
        var idsAgora = agora.Select(n => n.Id).ToHashSet();
        return (agora.Where(n => !idsAntes.Contains(n.Id)).ToList(), antes.Where(n => !idsAgora.Contains(n.Id)).Select(n => n.Id).ToList());
    }

    public static string HaQuanto(DateTimeOffset chegou, DateTimeOffset agora)
    {
        var passou = agora - chegou;
        if (passou < TimeSpan.FromMinutes(1)) return "agora";
        if (passou < TimeSpan.FromHours(1)) return $"há {(int)passou.TotalMinutes} min";
        if (passou < TimeSpan.FromDays(1)) return $"há {(int)passou.TotalHours} h";
        return chegou.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
    }

    /// O texto do painel quando não dá para ler as notificações; nulo quando dá.
    public static string? Texto(EstadoNotificacoes estado) => estado switch
    {
        EstadoNotificacoes.SemIdentidade =>
            "Ligue o modo de desenvolvedor do Windows (Configurações > Sistema > Para desenvolvedores) para o Vitral mostrar as notificações.",
        EstadoNotificacoes.SemPermissao =>
            "O Windows não deixou o Vitral ler as notificações. Permita em Configurações > Privacidade > Notificações.",
        _ => null,
    };
}

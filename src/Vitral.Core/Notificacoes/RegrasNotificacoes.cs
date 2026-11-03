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

    /// O pacote esparso aponta para a pasta do Vitral.exe (spec §2): registra só se ainda não está registrado com ela.
    public static bool PrecisaRegistrar(string? pastaRegistrada, string pastaAtual) =>
        pastaRegistrada is null
        || !string.Equals(pastaRegistrada.TrimEnd('\\', '/'), pastaAtual.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// Windows quieto (spec §4): os apps cujo balão o Vitral desliga. Fica de fora quem já estava com o balão desligado
    /// (escolha do usuário, que o Vitral não devolve) e as exceções abaixo.
    public static IReadOnlyList<string> ParaCalar(IEnumerable<(string AppId, int? ShowBanner)> apps) =>
        apps.Where(a => a.ShowBanner != 0 && !NuncaCalar(a.AppId)).Select(a => a.AppId).ToList();

    // Ficam com o balão do Windows (revisão final da 0.15.0): o que precisa dos botões do balão (chamadas no Celular,
    // alarmes, Teams), os avisos do próprio Windows e o próprio Vitral. O Claude é calado: os avisos dele vêm pelos ganchos
    // no notch, e o balão do Windows era repetição (pedido do Gustavo, 02/11).
    private static readonly string[] Excecoes =
        ["Vitral_", "Vitral.Dev_", "Microsoft.YourPhone_", "Microsoft.WindowsAlarms_", "Windows.SystemToast.", "MSTeams_", "com.squirrel.Teams."];

    private static bool NuncaCalar(string appId) => Excecoes.Any(e => appId.StartsWith(e, StringComparison.OrdinalIgnoreCase));

    /// Cala o Windows só quando o Vitral de fato mostra as notificações: lendo (a primeira leitura deu certo) e com o notch
    /// na tela. Senão, o usuário ficaria sem balão e sem notch.
    public static bool DeveCalar(EstadoNotificacoes estado, bool leituraFunciona, bool notchNaTela) =>
        estado == EstadoNotificacoes.Ligado && leituraFunciona && notchNaTela;

    /// O que o Vitral calou e ainda não devolveu (gravado no config): devolve na saída e, depois de uma queda, ao abrir.
    public static IReadOnlyList<string> ParaDevolver(IReadOnlyList<string>? gravados) => gravados ?? [];

    /// O gravado no config e o marcado no registro do Windows: uma versão antiga do Vitral que regrave o config sem a lista
    /// não faz o balão ficar calado para sempre.
    public static IReadOnlyList<string> ParaDevolver(IReadOnlyList<string>? gravados, IEnumerable<string> marcados) =>
        ParaDevolver(gravados).Concat(marcados).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

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

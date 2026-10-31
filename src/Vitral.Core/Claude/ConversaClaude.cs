using System.Text.RegularExpressions;

namespace Vitral.Core.Claude;

/// A conversa do app do Claude de onde veio um aviso: o gancho herda CLAUDE_CODE_HOST_SESSION_ID ("local_…") da sessão,
/// e o app abre a conversa pelo link "claude://claude.ai/epitaxy/&lt;id&gt;" (o mesmo das listas de sessões do app).
public static partial class ConversaClaude
{
    public const string VariavelDoApp = "CLAUDE_CODE_HOST_SESSION_ID";

    /// O link da conversa; null sem id ou com um id fora do formato do app (o clique só traz o app para a frente).
    public static string? Link(string? appId) =>
        appId is not null && IdDoApp().IsMatch(appId) ? $"claude://claude.ai/epitaxy/{appId}" : null;

    /// A conversa do aviso está aberta numa janela do Claude que é a da frente no monitor dela (pedido do Gustavo,
    /// 30/10: com várias sessões, só a que está à vista deixa de avisar). Sem id não dá para saber: conta como fora.
    public static bool AVista(string? appId, IEnumerable<JanelaClaude> janelas) =>
        appId is not null && IdDoApp().IsMatch(appId)
        && janelas.Any(j => j.NaFrente && j.Enderecos.Any(e => Uri.TryCreate(e, UriKind.Absolute, out var u)
            && u.Host.Equals("claude.ai", StringComparison.OrdinalIgnoreCase)
            && u.AbsolutePath.TrimEnd('/').EndsWith("/" + appId, StringComparison.Ordinal)));

    [GeneratedRegex(@"^local_[A-Za-z0-9-]{1,64}$")] private static partial Regex IdDoApp();
}

/// Uma janela do app do Claude: se é a da frente no monitor dela (não minimizada nem coberta) e os endereços dos
/// documentos que ela mostra (pela acessibilidade do Windows).
public sealed record JanelaClaude(bool NaFrente, IReadOnlyList<string> Enderecos);

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

    [GeneratedRegex(@"^local_[A-Za-z0-9-]{1,64}$")] private static partial Regex IdDoApp();
}

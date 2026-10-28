namespace Vitral.App.Agenda;

/// Identificação do Vitral no Google e na Microsoft (guia em docs/agenda-cadastro.md). Em app de computador estes
/// valores não são segredo: quem autoriza o acesso é a pessoa, no navegador. Vazio = provedor indisponível (o botão
/// daquele provedor fica desligado).
internal static class Credenciais
{
    public const string GoogleClientId = "281945787259-eqkfrl8h3s596vgqfri2b2dcceijavb1.apps.googleusercontent.com";
    public const string GoogleClientSecret = "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";
    public const string MicrosoftClientId = "";

    public static bool GoogleOk => GoogleClientId.Length > 0 && GoogleClientSecret.Length > 0;
    public static bool MicrosoftOk => MicrosoftClientId.Length > 0;
}

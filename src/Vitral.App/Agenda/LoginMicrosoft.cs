using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Vitral.Core.Agenda;

namespace Vitral.App.Agenda;

/// Login na Microsoft (spec da agenda §5.1): MSAL, contas pessoais e de organização, navegador do sistema, só leitura da
/// agenda. O cache de tokens do MSAL fica no cofre (contas\microsoft.bin, DPAPI pela extensão do MSAL).
internal sealed class LoginMicrosoft(CofreTokens cofre)
{
    private static readonly string[] Escopos = ["Calendars.Read"];
    private IPublicClientApplication? _app;

    private async Task<IPublicClientApplication> App()
    {
        if (_app is not null) return _app;
        var app = PublicClientApplicationBuilder.Create(Credenciais.MicrosoftClientId)
            .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
            .WithRedirectUri("http://localhost")
            .Build();
        var propriedades = new StorageCreationPropertiesBuilder("microsoft.bin", cofre.Pasta).Build(); // no Windows: DPAPI
        (await MsalCacheHelper.CreateAsync(propriedades)).RegisterCache(app.UserTokenCache);
        return _app = app;
    }

    /// Abre o navegador para a pessoa autorizar; devolve o e-mail, ou null se ela desistir. Erro do provedor (como a
    /// organização exigir aprovação do administrador) sobe como <see cref="InvalidOperationException"/> com o texto
    /// para a janela de contas.
    public async Task<string?> Entrar(CancellationToken cancelar)
    {
        var app = await App();
        // Como no Google: 5 min sem resposta no navegador conta como desistência.
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        limite.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            var resultado = await app.AcquireTokenInteractive(Escopos).WithUseEmbeddedWebView(false)
                .WithPrompt(Prompt.SelectAccount).ExecuteAsync(limite.Token);
            return resultado.Account.Username;
        }
        catch (MsalClientException e) when (e.ErrorCode == MsalError.AuthenticationCanceledError) { return null; }
        catch (OperationCanceledException) { return null; }
        catch (MsalServiceException e) when (e.Message.Contains("AADSTS65001") || e.Message.Contains("AADSTS90094"))
        {
            throw new InvalidOperationException("A organização precisa aprovar o Vitral (peça ao administrador do Microsoft 365).", e);
        }
        catch (MsalException e) { throw new InvalidOperationException(e.Message, e); }
    }

    /// O acesso atual da conta, sem tela; se o MSAL precisar de tela, a conta precisa entrar de novo.
    public Func<CancellationToken, Task<string>> Token(string email) => async cancelar =>
    {
        var app = await App();
        var conta = (await app.GetAccountsAsync()).FirstOrDefault(c => string.Equals(c.Username, email, StringComparison.OrdinalIgnoreCase))
            ?? throw new ContaPrecisaEntrarException();
        try { return (await app.AcquireTokenSilent(Escopos, conta).ExecuteAsync(cancelar)).AccessToken; }
        catch (MsalUiRequiredException) { throw new ContaPrecisaEntrarException(); }
    };

    /// Tira a conta do cache (o acesso deixa de existir nesta máquina).
    public async Task Sair(string email)
    {
        var app = await App();
        foreach (var conta in (await app.GetAccountsAsync()).Where(c => string.Equals(c.Username, email, StringComparison.OrdinalIgnoreCase)))
            await app.RemoveAsync(conta);
    }
}

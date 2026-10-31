using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Json;
using Google.Apis.Util.Store;
using Vitral.Core.Agenda;

namespace Vitral.App.Agenda;

/// Login no Google (spec da agenda §5.1): navegador do sistema, retorno pelo endereço local, só leitura da agenda. O
/// acesso fica no cofre, com a chave "google-&lt;e-mail&gt;".
internal sealed class LoginGoogle(CofreTokens cofre, HttpClient http)
{
    private static readonly string[] Escopos = ["https://www.googleapis.com/auth/calendar.readonly"];

    private const string PaginaDeVolta = """
        <html><head><meta charset="utf-8"><title>Vitral</title></head>
        <body style="font-family:Segoe UI,sans-serif;background:#0a0f20;color:#edeff7;text-align:center;padding-top:80px">
        <h2>Pronto, a agenda do Google está ligada.</h2><p>Pode fechar esta aba e voltar ao Vitral.</p></body></html>
        """;

    private readonly DepositoNoCofre _deposito = new(cofre);

    private GoogleAuthorizationCodeFlow Fluxo() => new(new GoogleAuthorizationCodeFlow.Initializer
    {
        ClientSecrets = new ClientSecrets { ClientId = Credenciais.GoogleClientId, ClientSecret = Credenciais.GoogleClientSecret },
        Scopes = Escopos,
        DataStore = _deposito,
        // Sempre a tela de consentimento: com o acesso ainda válido, o Google às vezes não manda um refresh_token novo, e
        // "Entrar de novo" gravaria um acesso que vence em 1 h sem poder renovar.
        Prompt = "consent",
    });

    /// Abre o navegador para a pessoa autorizar; devolve o e-mail da conta (o id da agenda principal), ou null se ela
    /// desistir (5 min sem resposta).
    public async Task<string?> Entrar(CancellationToken cancelar)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        limite.CancelAfter(TimeSpan.FromMinutes(5));
        var fluxo = Fluxo();
        string temporaria = "novo-" + Guid.NewGuid().ToString("N");
        try
        {
            var credencial = await new AuthorizationCodeInstalledApp(fluxo, new LocalServerCodeReceiver(PaginaDeVolta))
                .AuthorizeAsync(temporaria, limite.Token);
            using var pedido = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/calendars/primary");
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credencial.Token.AccessToken);
            using var resposta = await http.SendAsync(pedido, limite.Token);
            if (!resposta.IsSuccessStatusCode)
                throw new InvalidOperationException(RegrasAgenda.ErroDoGoogle(resposta.StatusCode, await resposta.Content.ReadAsStringAsync(limite.Token)));
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(limite.Token));
            string email = doc.RootElement.GetProperty("id").GetString()!;
            await _deposito.StoreAsync(email, credencial.Token); // passa da chave temporária para a do e-mail
            return email;
        }
        // O tempo do HttpClient (e não o Cancelar nem os 5 min) estourou: o Google não respondeu.
        catch (OperationCanceledException) when (!limite.IsCancellationRequested) { throw new InvalidOperationException(RegrasAgenda.ErroDoGoogle(null, null)); }
        catch (OperationCanceledException) { return null; }
        catch (HttpRequestException e) { throw new InvalidOperationException(RegrasAgenda.ErroDoGoogle(e.StatusCode, null), e); }
        catch (TokenResponseException e) when (e.Error?.Error == "access_denied")
        {
            // Cancelou no consentimento, ou o e-mail não está entre os usuários de teste do app.
            throw new InvalidOperationException("O Google recusou o acesso. Se o app está em teste no Google Cloud, o e-mail precisa estar em \"Usuários de teste\".", e);
        }
        catch (TokenResponseException e) { throw new InvalidOperationException($"O Google recusou o login: {e.Error?.ErrorDescription ?? e.Message}", e); }
        finally { await _deposito.DeleteAsync<TokenResponse>(temporaria); }
    }

    /// O acesso atual da conta (renovado quando vence); sem renovação possível, a conta precisa entrar de novo.
    public Func<CancellationToken, Task<string>> Token(string email) => async cancelar =>
    {
        var fluxo = Fluxo();
        var token = await fluxo.LoadTokenAsync(email, cancelar) ?? throw new ContaPrecisaEntrarException();
        try { return await new UserCredential(fluxo, email, token).GetAccessTokenForRequestAsync(cancellationToken: cancelar); }
        // Só a recusa do Google pede para entrar de novo; erro passageiro dele conta como sem conexão.
        catch (TokenResponseException e) when (e.Error?.Error == "invalid_grant") { throw new ContaPrecisaEntrarException(); }
        catch (TokenResponseException e) { throw new HttpRequestException(e.Message, e); }
        catch (InvalidOperationException) { throw new ContaPrecisaEntrarException(); } // acesso vencido sem como renovar
    };

    /// Revoga no Google e apaga do cofre.
    public async Task Sair(string email)
    {
        var fluxo = Fluxo();
        try
        {
            if (await fluxo.LoadTokenAsync(email, CancellationToken.None) is { } token)
                await fluxo.RevokeTokenAsync(email, token.RefreshToken ?? token.AccessToken, CancellationToken.None);
        }
        catch (Exception e) when (e is HttpRequestException or TokenResponseException) { /* sem rede: apaga mesmo assim */ }
        await _deposito.DeleteAsync<TokenResponse>(email);
    }

    /// O depósito de tokens da biblioteca do Google, gravando no cofre (DPAPI) em vez de JSON aberto no disco.
    private sealed class DepositoNoCofre(CofreTokens cofre) : IDataStore
    {
        private static string Chave(string chave) => "google-" + chave;

        public Task StoreAsync<T>(string key, T value)
        {
            cofre.Gravar(Chave(key), Encoding.UTF8.GetBytes(NewtonsoftJsonSerializer.Instance.Serialize(value)));
            return Task.CompletedTask;
        }

        public Task DeleteAsync<T>(string key)
        {
            cofre.Apagar(Chave(key));
            return Task.CompletedTask;
        }

        public Task<T> GetAsync<T>(string key) => Task.FromResult(cofre.Ler(Chave(key)) is { } dados
            ? NewtonsoftJsonSerializer.Instance.Deserialize<T>(Encoding.UTF8.GetString(dados))
            : default!);

        public Task ClearAsync()
        {
            cofre.ApagarComecandoCom("google-");
            return Task.CompletedTask;
        }
    }
}

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Vitral.Core.Agenda;

/// Chamada autenticada que devolve o JSON: 401 vira "entrar de novo"; outro erro, <see cref="HttpRequestException"/>.
internal static class Rest
{
    public static async Task<JsonDocument> Buscar(HttpClient http, string url, Func<CancellationToken, Task<string>> token,
        CancellationToken cancelar, Action<HttpRequestMessage>? ajustar = null)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, url);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await token(cancelar));
        ajustar?.Invoke(pedido);
        using var resposta = await http.SendAsync(pedido, cancelar);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized) throw new ContaPrecisaEntrarException();
        resposta.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(cancelar), cancellationToken: cancelar);
    }

    public static string Utc(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    public static string? Texto(this JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static bool Verdade(this JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.True;

    public static Uri? Endereco(string? texto) => Uri.TryCreate(texto, UriKind.Absolute, out var u) ? u : null;
}

/// Agenda do Google pela API REST (spec da agenda §5.1; plano: REST em vez do Calendar.v3): a agenda principal da conta,
/// com os eventos repetidos já abertos em ocorrências, página por página.
public sealed class LeitorGoogle(ContaAgenda conta, HttpClient http, Func<CancellationToken, Task<string>> token,
    TimeZoneInfo? fuso = null) : IFonteAgenda
{
    private const string Base = "https://www.googleapis.com/calendar/v3";
    private readonly TimeZoneInfo _fuso = fuso ?? TimeZoneInfo.Local;

    public ContaAgenda Conta { get; } = conta;

    public async Task<IReadOnlyList<Compromisso>> Ler(DateTimeOffset de, DateTimeOffset ate, CancellationToken cancelar)
    {
        // Só a agenda principal: é onde o Google põe o que a pessoa criou e os convites que recebeu; as compartilhadas
        // (de colegas, de equipes) trariam compromissos em que ela não está (conferência com o Gustavo, 28/10).
        var compromissos = new List<Compromisso>();
        foreach (var agenda in new[] { "primary" })
        {
            string? pagina = null;
            do
            {
                string url = $"{Base}/calendars/{Uri.EscapeDataString(agenda)}/events?timeMin={Rest.Utc(de)}&timeMax={Rest.Utc(ate)}"
                    + "&singleEvents=true&orderBy=startTime&maxResults=250" + (pagina is null ? "" : $"&pageToken={Uri.EscapeDataString(pagina)}");
                using var doc = await Rest.Buscar(http, url, token, cancelar);
                if (doc.RootElement.TryGetProperty("items", out var itens))
                    foreach (var e in itens.EnumerateArray())
                        if (Converter(agenda, e) is { } c) compromissos.Add(c);
                pagina = doc.RootElement.Texto("nextPageToken");
            } while (pagina is not null);
        }
        return compromissos;
    }

    private Compromisso? Converter(string agenda, JsonElement e)
    {
        if (e.Texto("id") is not { } id || !e.TryGetProperty("start", out var inicio) || !e.TryGetProperty("end", out var fim)) return null;
        bool diaInteiro = inicio.Texto("date") is not null;
        DateTimeOffset de, ate;
        if (diaInteiro)
            (de, ate) = RegrasAgenda.DiaInteiro(DateOnly.Parse(inicio.Texto("date")!, CultureInfo.InvariantCulture),
                DateOnly.Parse(fim.Texto("date")!, CultureInfo.InvariantCulture), _fuso);
        else
        {
            de = DateTimeOffset.Parse(inicio.Texto("dateTime")!, CultureInfo.InvariantCulture);
            ate = DateTimeOffset.Parse(fim.Texto("dateTime")!, CultureInfo.InvariantCulture);
        }
        bool recusado = e.TryGetProperty("attendees", out var convidados) && convidados.EnumerateArray()
            .Any(a => a.Verdade("self") && a.Texto("responseStatus") == "declined");
        var link = Rest.Endereco(e.Texto("hangoutLink")) ?? VideoDaConferencia(e) ?? RegrasAgenda.ExtrairLink(e.Texto("location"), e.Texto("description"));
        return new Compromisso($"{agenda}/{id}", Conta, e.Texto("summary") ?? "(sem título)", de, ate, diaInteiro, e.Texto("location"),
            link, Rest.Endereco(e.Texto("htmlLink")), recusado, e.Texto("status") == "cancelled");
    }

    private static Uri? VideoDaConferencia(JsonElement e) =>
        e.TryGetProperty("conferenceData", out var c) && c.TryGetProperty("entryPoints", out var pontos)
            ? pontos.EnumerateArray().Where(p => p.Texto("entryPointType") == "video").Select(p => Rest.Endereco(p.Texto("uri"))).FirstOrDefault(u => u is not null)
            : null;
}

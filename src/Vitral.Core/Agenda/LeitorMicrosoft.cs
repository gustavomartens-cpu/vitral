using System.Globalization;
using System.Text.Json;

namespace Vitral.Core.Agenda;

/// Agenda da Microsoft pelo Graph (spec da agenda §5.1): <c>/me/calendarView</c> no intervalo, em UTC, página por página.
public sealed class LeitorMicrosoft(ContaAgenda conta, HttpClient http, Func<CancellationToken, Task<string>> token,
    TimeZoneInfo? fuso = null) : IFonteAgenda
{
    private readonly TimeZoneInfo _fuso = fuso ?? TimeZoneInfo.Local;

    public ContaAgenda Conta { get; } = conta;

    public async Task<IReadOnlyList<Compromisso>> Ler(DateTimeOffset de, DateTimeOffset ate, CancellationToken cancelar)
    {
        var compromissos = new List<Compromisso>();
        string? url = $"https://graph.microsoft.com/v1.0/me/calendarView?startDateTime={Rest.Utc(de)}&endDateTime={Rest.Utc(ate)}&$top=100";
        while (url is not null)
        {
            using var doc = await Rest.Buscar(http, url, token, cancelar, p => p.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\""));
            foreach (var e in doc.RootElement.GetProperty("value").EnumerateArray())
                if (Converter(e) is { } c) compromissos.Add(c);
            url = doc.RootElement.Texto("@odata.nextLink");
        }
        return compromissos;
    }

    private Compromisso? Converter(JsonElement e)
    {
        if (e.Texto("id") is not { } id || !e.TryGetProperty("start", out var inicio) || !e.TryGetProperty("end", out var fim)) return null;
        var de = Utc(inicio.Texto("dateTime"));
        var ate = Utc(fim.Texto("dateTime"));
        bool diaInteiro = e.Verdade("isAllDay");
        if (diaInteiro) (de, ate) = RegrasAgenda.DiaInteiro(DateOnly.FromDateTime(de.UtcDateTime), DateOnly.FromDateTime(ate.UtcDateTime), _fuso);
        string? local = e.TryGetProperty("location", out var l) ? l.Texto("displayName") : null;
        var link = (e.TryGetProperty("onlineMeeting", out var reuniao) && reuniao.ValueKind == JsonValueKind.Object
            ? Rest.Endereco(reuniao.Texto("joinUrl")) : null) ?? RegrasAgenda.ExtrairLink(local, e.Texto("bodyPreview"));
        bool recusado = e.TryGetProperty("responseStatus", out var resposta) && resposta.Texto("response") == "declined";
        return new Compromisso(id, Conta, e.Texto("subject") ?? "(sem título)", de, ate, diaInteiro, string.IsNullOrEmpty(local) ? null : local,
            link, Rest.Endereco(e.Texto("webLink")), recusado, e.Verdade("isCancelled"));
    }

    private static DateTimeOffset Utc(string? texto) =>
        new(DateTime.SpecifyKind(DateTime.Parse(texto!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc));
}

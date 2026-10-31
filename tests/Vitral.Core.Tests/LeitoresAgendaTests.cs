using System.Net;
using Vitral.Core.Agenda;

namespace Vitral.Core.Tests;

public sealed class LeitoresAgendaTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
    private static readonly ContaAgenda Google = new(ProvedorAgenda.Google, "g@x.com", "#4C7DF0");
    private static readonly ContaAgenda Microsoft = new(ProvedorAgenda.Microsoft, "m@x.com", "#2BA9E0");
    private static readonly DateTimeOffset De = new(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Ate = new(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(-3));

    /// Responde pelo trecho do endereço; guarda os pedidos.
    private sealed class Servidor(params (string Trecho, string Json)[] respostas) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Pedidos { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        /// Endereços com este trecho respondem 404 (a agenda saiu da conta).
        public string? Falhar { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken c)
        {
            Pedidos.Add(pedido);
            if (Status != HttpStatusCode.OK) return Task.FromResult(new HttpResponseMessage(Status));
            string url = pedido.RequestUri!.ToString();
            if (Falhar is not null && url.Contains(Falhar, StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var json = respostas.Where(r => url.Contains(r.Trecho, StringComparison.Ordinal)).Select(r => r.Json).FirstOrDefault()
                ?? throw new InvalidOperationException("sem resposta para " + url);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    private const string ListaGoogle = """
        { "items": [
          { "id": "g@x.com", "selected": true },
          { "id": "pt.brazilian#holiday@group.v.calendar.google.com", "selected": false },
          { "id": "oculta@group.calendar.google.com" }
        ] }
        """;

    private const string EventosGoogle1 = """
        { "nextPageToken": "pag2", "items": [
          { "id": "e1", "status": "confirmed", "summary": "Reunião com cliente", "htmlLink": "https://www.google.com/calendar/event?eid=e1",
            "start": { "dateTime": "2026-10-28T14:30:00-03:00" }, "end": { "dateTime": "2026-10-28T15:30:00-03:00" },
            "hangoutLink": "https://meet.google.com/abc-defg-hij",
            "attendees": [ { "email": "g@x.com", "self": true, "responseStatus": "accepted" } ] },
          { "id": "e2", "status": "confirmed", "summary": "Feriado", "start": { "date": "2026-11-02" }, "end": { "date": "2026-11-03" } },
          { "id": "e3", "status": "confirmed", "summary": "Zoom", "location": "Sala", "description": "Entre: https://us02web.zoom.us/j/999",
            "start": { "dateTime": "2026-10-29T10:00:00-03:00" }, "end": { "dateTime": "2026-10-29T11:00:00-03:00" } }
        ] }
        """;

    private const string EventosGoogle2 = """
        { "items": [
          { "id": "e4", "status": "confirmed", "summary": "Recusado", "start": { "dateTime": "2026-10-30T09:00:00-03:00" }, "end": { "dateTime": "2026-10-30T10:00:00-03:00" },
            "attendees": [ { "email": "g@x.com", "self": true, "responseStatus": "declined" } ] },
          { "id": "e5", "status": "cancelled", "summary": "Cancelado", "start": { "dateTime": "2026-10-30T11:00:00-03:00" }, "end": { "dateTime": "2026-10-30T12:00:00-03:00" } }
        ] }
        """;

    private const string Graph1 = """
        { "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/calendarView?startDateTime=x&$skiptoken=PAG2", "value": [
          { "id": "m1", "subject": "Daily", "isAllDay": false, "isCancelled": false, "webLink": "https://outlook.office365.com/owa/?itemid=m1",
            "start": { "dateTime": "2026-10-28T17:30:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-10-28T18:00:00.0000000", "timeZone": "UTC" },
            "responseStatus": { "response": "accepted" }, "location": { "displayName": "Microsoft Teams" }, "bodyPreview": "",
            "onlineMeeting": { "joinUrl": "https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc/0" } },
          { "id": "m2", "subject": "Folga", "isAllDay": true, "isCancelled": false,
            "start": { "dateTime": "2026-11-05T00:00:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-11-06T00:00:00.0000000", "timeZone": "UTC" },
            "responseStatus": { "response": "organizer" }, "location": { "displayName": "" }, "bodyPreview": "" }
        ] }
        """;

    private const string Graph2 = """
        { "value": [
          { "id": "m3", "subject": "Recusada", "isAllDay": false, "isCancelled": false,
            "start": { "dateTime": "2026-10-29T12:00:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-10-29T13:00:00.0000000", "timeZone": "UTC" },
            "responseStatus": { "response": "declined" }, "location": { "displayName": "" }, "bodyPreview": "" },
          { "id": "m4", "subject": "Cancelada", "isAllDay": false, "isCancelled": true,
            "start": { "dateTime": "2026-10-29T14:00:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-10-29T15:00:00.0000000", "timeZone": "UTC" },
            "responseStatus": { "response": "accepted" }, "location": { "displayName": "" }, "bodyPreview": "" }
        ] }
        """;

    private static Task<string> Token(CancellationToken _) => Task.FromResult("tok");

    private static (LeitorGoogle Leitor, Servidor Servidor) LeitorDoGoogle()
    {
        var s = new Servidor(("calendarList", ListaGoogle), ("pageToken=pag2", EventosGoogle2), ("/events", EventosGoogle1));
        return (new LeitorGoogle(Google, new HttpClient(s), Token, Brasilia), s);
    }

    private static (LeitorMicrosoft Leitor, Servidor Servidor) LeitorDaMicrosoft()
    {
        var s = new Servidor(("skiptoken=PAG2", Graph2), ("calendarView", Graph1));
        return (new LeitorMicrosoft(Microsoft, new HttpClient(s), Token, Brasilia), s);
    }

    [Fact]
    public async Task Google_converte_eventos()
    {
        var (leitor, servidor) = LeitorDoGoogle();
        var lista = (await leitor.Ler(De, Ate, CancellationToken.None)).ToDictionary(c => c.Id.Split('/')[^1]);
        var e1 = lista["e1"];
        Assert.Equal(("Reunião com cliente", new DateTimeOffset(2026, 10, 28, 14, 30, 0, TimeSpan.FromHours(-3))), (e1.Titulo, e1.Inicio));
        Assert.Equal("https://meet.google.com/abc-defg-hij", e1.LinkReuniao?.ToString());
        Assert.Equal("https://www.google.com/calendar/event?eid=e1", e1.LinkEvento?.ToString());
        Assert.Equal(Google, e1.Conta);
        Assert.True(lista["e2"].DiaInteiro);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.FromHours(-3)), lista["e2"].Inicio);
        Assert.Equal("https://us02web.zoom.us/j/999", lista["e3"].LinkReuniao?.ToString());
        Assert.True(lista["e4"].Recusado);
        Assert.True(lista["e5"].Cancelado);
        Assert.All(servidor.Pedidos, p => Assert.Equal("Bearer tok", p.Headers.Authorization?.ToString()));
    }

    [Fact]
    public async Task Graph_converte_eventos()
    {
        var (leitor, servidor) = LeitorDaMicrosoft();
        var lista = (await leitor.Ler(De, Ate, CancellationToken.None)).ToDictionary(c => c.Id);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 17, 30, 0, TimeSpan.Zero), lista["m1"].Inicio);
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc/0", lista["m1"].LinkReuniao?.ToString());
        Assert.True(lista["m2"].DiaInteiro);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.FromHours(-3)), lista["m2"].Inicio);
        Assert.True(lista["m3"].Recusado);
        Assert.True(lista["m4"].Cancelado);
        Assert.Contains(servidor.Pedidos, p => p.Headers.TryGetValues("Prefer", out var v) && v.Single() == "outlook.timezone=\"UTC\"");
    }

    [Fact]
    public async Task Le_todas_as_paginas()
    {
        var (google, _) = LeitorDoGoogle();
        Assert.Equal(5, (await google.Ler(De, Ate, CancellationToken.None)).Count);
        var (microsoft, _) = LeitorDaMicrosoft();
        Assert.Equal(4, (await microsoft.Ler(De, Ate, CancellationToken.None)).Count);
    }

    [Fact]
    // Conferência com o Gustavo (28/10): só os compromissos em que a pessoa está — a agenda principal, onde o Google põe
    // o que ela criou e os convites; as agendas compartilhadas (de colegas, de equipes) ficam de fora.
    public async Task Le_so_a_agenda_principal_do_Google()
    {
        var (leitor, servidor) = LeitorDoGoogle();
        await leitor.Ler(De, Ate, CancellationToken.None);
        Assert.DoesNotContain(servidor.Pedidos, p => p.RequestUri!.AbsolutePath.Contains("calendarList"));
        var agendas = servidor.Pedidos.Select(p => p.RequestUri!.AbsolutePath).Distinct().ToList();
        Assert.Equal(["/calendar/v3/calendars/primary/events"], agendas);
    }

    private const string Unico = """
        { "items": [ { "id": "x1", "iCalUID": "u1@google.com", "status": "confirmed", "summary": "Convite",
            "start": { "dateTime": "2026-10-28T14:30:00-03:00" }, "end": { "dateTime": "2026-10-28T15:30:00-03:00" } } ] }
        """;

    private const string OutroDaFamilia = """
        { "items": [
          { "id": "x2", "iCalUID": "u1@google.com", "status": "confirmed", "summary": "Convite",
            "start": { "dateTime": "2026-10-28T14:30:00-03:00" }, "end": { "dateTime": "2026-10-28T15:30:00-03:00" } },
          { "id": "x3", "iCalUID": "u2@google.com", "status": "confirmed", "summary": "Aniversário da Ana",
            "start": { "date": "2026-11-05" }, "end": { "date": "2026-11-06" } } ] }
        """;

    [Fact]
    public async Task Le_as_agendas_escolhidas_alem_da_principal_sem_repetir()
    {
        var s = new Servidor(("/calendars/primary/events", Unico), ("/calendars/familia%40group.calendar.google.com/events", OutroDaFamilia));
        var leitor = new LeitorGoogle(Google, new HttpClient(s), Token, Brasilia, ["familia@group.calendar.google.com"]);
        var lista = await leitor.Ler(De, Ate, CancellationToken.None);
        Assert.Equal(["Convite", "Aniversário da Ana"], lista.Select(c => c.Titulo));
    }

    [Fact]
    public async Task Agenda_escolhida_que_sumiu_nao_derruba_a_leitura()
    {
        var s = new Servidor(("/calendars/primary/events", Unico)) { Falhar = "familia" };
        var leitor = new LeitorGoogle(Google, new HttpClient(s), Token, Brasilia, ["familia@group.calendar.google.com"]);
        Assert.Single(await leitor.Ler(De, Ate, CancellationToken.None));
    }

    [Fact]
    public async Task Lista_as_agendas_da_conta_com_a_principal_primeiro()
    {
        const string lista = """
            { "items": [
              { "id": "familia@group.calendar.google.com", "summary": "Família" },
              { "id": "g@x.com", "summary": "g@x.com", "primary": true },
              { "id": "pt.brazilian#holiday@group.v.calendar.google.com", "summary": "Feriados no Brasil", "summaryOverride": "Feriados" } ] }
            """;
        var s = new Servidor(("calendarList", lista));
        var agendas = await LeitorGoogle.ListarAgendas(new HttpClient(s), Token, CancellationToken.None);
        Assert.Equal([("g@x.com", true), ("familia@group.calendar.google.com", false), ("pt.brazilian#holiday@group.v.calendar.google.com", false)],
            agendas.Select(a => (a.Id, a.Principal)));
        Assert.Equal("Feriados", agendas[2].Nome);
    }

    [Fact]
    public async Task Http_401_pede_para_entrar()
    {
        var (google, sg) = LeitorDoGoogle();
        sg.Status = HttpStatusCode.Unauthorized;
        await Assert.ThrowsAsync<ContaPrecisaEntrarException>(() => google.Ler(De, Ate, CancellationToken.None));
        var (microsoft, sm) = LeitorDaMicrosoft();
        sm.Status = HttpStatusCode.InternalServerError;
        await Assert.ThrowsAsync<HttpRequestException>(() => microsoft.Ler(De, Ate, CancellationToken.None));
    }

    // Revisão final, Important 4: links vindos do convite só se forem da web (nada de file:, UNC, ms-msdt:...).
    [Fact]
    public async Task Link_que_nao_e_da_web_fica_de_fora()
    {
        const string evento = """
            { "items": [ { "id": "x", "status": "confirmed", "summary": "Armadilha", "htmlLink": "file:///C:/Windows/System32/calc.exe",
              "hangoutLink": "ms-msdt:/id PCWDiagnostic", "start": { "dateTime": "2026-10-28T14:30:00-03:00" }, "end": { "dateTime": "2026-10-28T15:30:00-03:00" } } ] }
            """;
        var s = new Servidor(("/events", evento));
        var c = Assert.Single(await new LeitorGoogle(Google, new HttpClient(s), Token, Brasilia).Ler(De, Ate, CancellationToken.None));
        Assert.Null(c.LinkReuniao);
        Assert.Null(c.LinkEvento);
    }
}

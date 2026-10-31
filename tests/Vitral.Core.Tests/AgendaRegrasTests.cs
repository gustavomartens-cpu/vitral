using Vitral.Core.Agenda;
using Vitral.Core.Configuracao;

namespace Vitral.Core.Tests;

public sealed class AgendaRegrasTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
    private static readonly ContaAgenda Google = new(ProvedorAgenda.Google, "g@x.com", "#4C7DF0");
    private static readonly DateTimeOffset T = new(2026, 10, 28, 14, 30, 0, TimeSpan.FromHours(-3));

    private static Compromisso C(string id, DateTimeOffset inicio, double horas = 1, string titulo = "Reunião", bool diaInteiro = false,
        bool recusado = false, bool cancelado = false) =>
        new(id, Google, titulo, inicio, inicio.AddHours(horas), diaInteiro, null, null, null, recusado, cancelado);

    [Fact]
    public void Juntar_ordena_e_tira_cancelados_e_recusados()
    {
        var (dia, _) = RegrasAgenda.DiaInteiro(new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29), Brasilia);
        var lista = RegrasAgenda.Juntar([
            C("b", T, titulo: "B"), C("a", T, titulo: "A"), C("c", T.AddHours(-2)), C("x", T, cancelado: true),
            C("r", T, recusado: true), C("d", dia, 24, diaInteiro: true),
        ]);
        Assert.Equal(["d", "c", "a", "b"], lista.Select(c => c.Id));
    }

    [Theory]
    [InlineData("Link: https://meet.google.com/abc-defg-hij obrigado", "https://meet.google.com/abc-defg-hij")]
    [InlineData("Entrar: https://teams.microsoft.com/l/meetup-join/19%3ameeting_x/0?context=y fim", "https://teams.microsoft.com/l/meetup-join/19%3ameeting_x/0?context=y")]
    [InlineData("https://us02web.zoom.us/j/123456?pwd=abc", "https://us02web.zoom.us/j/123456?pwd=abc")]
    [InlineData("sem link aqui https://exemplo.com", null)]
    public void ExtrairLink_pela_descricao(string descricao, string? esperado) =>
        Assert.Equal(esperado, RegrasAgenda.ExtrairLink(null, descricao)?.ToString());

    [Fact]
    public void ExtrairLink_prefere_o_local_e_o_Meet()
    {
        Assert.Equal("https://meet.google.com/aaa-bbbb-ccc",
            RegrasAgenda.ExtrairLink("https://meet.google.com/aaa-bbbb-ccc", "https://us02web.zoom.us/j/1")?.ToString());
    }

    [Fact]
    public void Aviso_na_antecedencia_e_so_uma_vez()
    {
        var c = C("a", T);
        var avisados = new HashSet<(string, DateTimeOffset)>();
        Assert.Empty(RegrasAgenda.ProximosAvisos([c], T.AddMinutes(-11), TimeSpan.FromMinutes(10), avisados));
        var avisos = RegrasAgenda.ProximosAvisos([c], T.AddMinutes(-10), TimeSpan.FromMinutes(10), avisados);
        Assert.Equal(["a"], avisos.Select(a => a.Id));
        Assert.Empty(RegrasAgenda.ProximosAvisos([c], T.AddMinutes(-5), TimeSpan.FromMinutes(10), avisados));
    }

    [Fact]
    public void Remarcado_avisa_de_novo()
    {
        var avisados = new HashSet<(string, DateTimeOffset)>();
        RegrasAgenda.ProximosAvisos([C("a", T)], T.AddMinutes(-10), TimeSpan.FromMinutes(10), avisados);
        var remarcado = C("a", T.AddMinutes(30));
        Assert.Single(RegrasAgenda.ProximosAvisos([remarcado], T.AddMinutes(20), TimeSpan.FromMinutes(10), avisados));
    }

    [Fact]
    public void Dia_inteiro_nao_avisa()
    {
        var (inicio, _) = RegrasAgenda.DiaInteiro(new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29), Brasilia);
        Assert.Empty(RegrasAgenda.ProximosAvisos([C("d", inicio, 24, diaInteiro: true)], inicio, TimeSpan.FromMinutes(10), new HashSet<(string, DateTimeOffset)>()));
    }

    [Fact]
    public void Suspensao_avisa_uma_vez()
    {
        var c = C("a", T);
        var avisados = new HashSet<(string, DateTimeOffset)>();
        Assert.Single(RegrasAgenda.ProximosAvisos([c], T.AddMinutes(20), TimeSpan.FromMinutes(10), avisados)); // ainda não acabou
        Assert.Empty(RegrasAgenda.ProximosAvisos([C("b", T)], T.AddHours(2), TimeSpan.FromMinutes(10), avisados)); // já acabou
    }

    [Fact]
    public void Dia_inteiro_vira_meia_noite_local()
    {
        var (inicio, fim) = RegrasAgenda.DiaInteiro(new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29), Brasilia);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.FromHours(-3)), inicio);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.FromHours(-3)), fim);
    }

    [Fact]
    public void Varios_dias_marcam_todos()
    {
        var (inicio, fim) = RegrasAgenda.DiaInteiro(new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 1), Brasilia);
        var c = new Compromisso("v", Google, "Viagem", inicio, fim, true, null, null, null, false, false);
        Assert.Equal([28, 29, 30, 31], RegrasAgenda.DiasComCompromisso([c], 2026, 10, Brasilia).Order());
        Assert.Empty(RegrasAgenda.DiasComCompromisso([c], 2026, 11, Brasilia));
    }

    [Fact]
    public void DoDia_inclui_o_que_atravessa_a_meia_noite()
    {
        var noite = C("n", new DateTimeOffset(2026, 10, 28, 23, 0, 0, TimeSpan.FromHours(-3)), 2);
        Assert.Single(RegrasAgenda.DoDia([noite], new DateOnly(2026, 10, 28), Brasilia));
        Assert.Single(RegrasAgenda.DoDia([noite], new DateOnly(2026, 10, 29), Brasilia));
        Assert.Empty(RegrasAgenda.DoDia([noite], new DateOnly(2026, 10, 30), Brasilia));
    }

    [Fact]
    public void Cores_das_contas()
    {
        Assert.Equal("#4C7DF0", RegrasAgenda.CorDaConta(ProvedorAgenda.Google, 0));
        Assert.Equal("#2BA9E0", RegrasAgenda.CorDaConta(ProvedorAgenda.Microsoft, 0));
        Assert.Equal("#F2B544", RegrasAgenda.CorDaConta(ProvedorAgenda.Google, 1));
        Assert.Equal("#3DDBB5", RegrasAgenda.CorDaConta(ProvedorAgenda.Microsoft, 2));
    }

    [Fact]
    public void Config_sem_token()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "vitral-agenda-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ServicoConfig(Path.Combine(raiz, "config.json"));
            config.Gravar(config.Ler() with { Agenda = new AgendaConfig([Google], 15) });
            var texto = File.ReadAllText(Path.Combine(raiz, "config.json"));
            Assert.Contains("g@x.com", texto);
            Assert.DoesNotContain("token", texto, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(15, config.Ler().Agenda!.AntecedenciaMin);
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public void Agendas_escolhidas_ficam_no_config_por_conta()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "vitral-agenda-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ServicoConfig(Path.Combine(raiz, "config.json"));
            var outra = new ContaAgenda(ProvedorAgenda.Google, "outra@x.com", "#3DDBB5");
            var agenda = new AgendaConfig([Google, outra]).ComAgendas(Google, ["familia@group.calendar.google.com"]);
            config.Gravar(config.Ler() with { Agenda = agenda });
            var lido = config.Ler().Agenda!;
            Assert.Equal(["familia@group.calendar.google.com"], lido.AgendasDe(Google));
            Assert.Empty(lido.AgendasDe(outra));
            Assert.Empty(lido.ComAgendas(Google, []).AgendasDe(Google));
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }
}

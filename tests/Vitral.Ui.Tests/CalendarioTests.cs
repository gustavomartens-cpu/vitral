using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class CalendarioTests
{
    private readonly AgoraFalso _agora = new(new DateTime(2026, 10, 25, 10, 0, 0));

    [Fact]
    public void Outubro_de_2026_comeca_em_27_de_setembro()
    {
        var vm = new CalendarioViewModel(_agora);
        Assert.Equal(42, vm.Dias.Count);
        Assert.Equal(new DiaCalendario(27, false, false, Data: new DateOnly(2026, 9, 27)), vm.Dias[0]);
        Assert.Equal(new DiaCalendario(1, true, false, Data: new DateOnly(2026, 10, 1)), vm.Dias[4]);
        Assert.Equal(25, vm.Dias.Single(d => d.Hoje).Numero);
    }

    [Fact]
    public void Titulo_e_data_por_extenso()
    {
        var vm = new CalendarioViewModel(_agora);
        Assert.Equal("outubro de 2026", vm.Titulo);
        Assert.Equal("domingo, 25 de outubro de 2026", vm.DataPorExtenso);
    }

    [Fact]
    public void Navega_e_atravessa_o_ano()
    {
        var vm = new CalendarioViewModel(_agora);
        vm.ProximoCommand.Execute(null);
        vm.ProximoCommand.Execute(null);
        vm.ProximoCommand.Execute(null);
        Assert.Equal("janeiro de 2027", vm.Titulo);
        Assert.DoesNotContain(vm.Dias, d => d.Hoje);
        vm.AnteriorCommand.Execute(null);
        Assert.Equal("dezembro de 2026", vm.Titulo);
    }

    [Fact]
    public void Hoje_volta_ao_mes_atual()
    {
        var vm = new CalendarioViewModel(_agora);
        vm.AnteriorCommand.Execute(null);
        vm.HojeCommand.Execute(null);
        Assert.Equal("outubro de 2026", vm.Titulo);
    }

    [Fact]
    public void Reabrir_rele_o_dia_de_hoje()
    {
        var vm = new CalendarioViewModel(_agora);
        vm.ProximoCommand.Execute(null);
        _agora.Agora = new DateTime(2026, 11, 2);
        vm.Reabrir();
        Assert.Equal("novembro de 2026", vm.Titulo);
        Assert.Equal(2, vm.Dias.Single(d => d.Hoje).Numero);
    }

    // ---- Agenda (spec da agenda §5.4) ----

    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
    private static readonly Core.Agenda.ContaAgenda Google = new(Core.Agenda.ProvedorAgenda.Google, "g@x.com", "#4C7DF0");
    private static readonly DateTimeOffset Leitura = new(2026, 10, 25, 9, 40, 0, TimeSpan.FromHours(-3));

    private sealed class FonteFalsa : Core.Agenda.IFonteAgenda
    {
        public Core.Agenda.ContaAgenda Conta => Google;
        public Func<IReadOnlyList<Core.Agenda.Compromisso>> Resposta { get; set; } = () => [];
        public Task<IReadOnlyList<Core.Agenda.Compromisso>> Ler(DateTimeOffset de, DateTimeOffset ate, CancellationToken c) => Task.FromResult(Resposta());
    }

    private static Core.Agenda.Compromisso C(string id, int dia, int hora, double horas = 1, bool diaInteiro = false)
    {
        var inicio = new DateTimeOffset(2026, 10, dia, hora, 0, 0, TimeSpan.FromHours(-3));
        return new(id, Google, id, inicio, inicio.AddHours(horas), diaInteiro, null,
            id == "reuniao" ? new Uri("https://meet.google.com/abc-defg-hij") : null, null, false, false);
    }

    private static async Task<(CalendarioViewModel Vm, FonteFalsa Fonte, Core.Agenda.ServicoAgenda Agenda)> ComAgenda(AgoraFalso agora,
        params Core.Agenda.Compromisso[] lista)
    {
        var fonte = new FonteFalsa { Resposta = () => lista };
        var agenda = new Core.Agenda.ServicoAgenda(() => Leitura, Brasilia);
        agenda.DefinirFontes([fonte]);
        await agenda.Atualizar(CancellationToken.None);
        return (new CalendarioViewModel(agora, agenda), fonte, agenda);
    }

    [Fact]
    public async Task Dias_com_compromisso_marcados()
    {
        var (vm, _, _) = await ComAgenda(_agora, C("a", 25, 14), C("b", 28, 9));
        Assert.Equal([25, 28], vm.Dias.Where(d => d.TemCompromisso).Select(d => d.Numero));
        Assert.Equal(new DateOnly(2026, 10, 28), vm.Dias.Single(d => d.DoMes && d.Numero == 28).Data);
    }

    [Fact]
    public async Task Abre_em_hoje_e_troca_de_dia_pelo_clique()
    {
        var (vm, _, _) = await ComAgenda(_agora, C("a", 25, 14), C("reuniao", 28, 9));
        Assert.Equal(new DateOnly(2026, 10, 25), vm.DiaEscolhido);
        Assert.Equal(["a"], vm.DoDia.Select(i => i.Titulo));
        vm.EscolherDiaCommand.Execute(vm.Dias.Single(d => d.DoMes && d.Numero == 28));
        Assert.Equal(new DateOnly(2026, 10, 28), vm.DiaEscolhido);
        var item = Assert.Single(vm.DoDia);
        Assert.Equal(("09:00–10:00", "#4C7DF0", "https://meet.google.com/abc-defg-hij"), (item.Horario, item.Cor, item.LinkReuniao?.ToString()));
    }

    [Fact]
    public async Task Lista_do_dia_com_dia_inteiro_primeiro()
    {
        var feriado = new Core.Agenda.Compromisso("f", Google, "Feriado", new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.FromHours(-3)), true, null, null, null, false, false);
        var (vm, _, _) = await ComAgenda(_agora, C("a", 25, 8), feriado);
        Assert.Equal(["Feriado", "a"], vm.DoDia.Select(i => i.Titulo));
        Assert.Equal("Dia inteiro", vm.DoDia[0].Horario);
    }

    [Fact]
    public void Sem_contas_mostra_o_convite()
    {
        var agenda = new Core.Agenda.ServicoAgenda(() => Leitura, Brasilia);
        var vm = new CalendarioViewModel(_agora, agenda);
        Assert.False(vm.TemContas);
        Assert.Equal("Ligue sua agenda em Contas…", vm.Situacao);
        Assert.Null(new CalendarioViewModel(_agora).Situacao); // sem agenda nenhuma: como antes
    }

    [Fact]
    public async Task Com_dado_antigo_mostra_a_hora()
    {
        var (vm, fonte, agenda) = await ComAgenda(_agora, C("a", 25, 14));
        Assert.Null(vm.Situacao);
        fonte.Resposta = () => throw new HttpRequestException("fora");
        await agenda.Atualizar(CancellationToken.None);
        Assert.Equal("atualizado às 09:40", vm.Situacao);
        Assert.Equal(["a"], vm.DoDia.Select(i => i.Titulo));
    }

    [Fact]
    public async Task AbrirDia_vai_ao_mes_do_dia()
    {
        var (vm, _, _) = await ComAgenda(_agora);
        vm.AbrirDia(new DateOnly(2026, 11, 3));
        Assert.Equal("novembro de 2026", vm.Titulo);
        Assert.Equal(new DateOnly(2026, 11, 3), vm.DiaEscolhido);
    }
}

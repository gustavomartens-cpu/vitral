using Vitral.Core.Agenda;

namespace Vitral.Core.Tests;

public sealed class ServicoAgendaTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
    private static readonly ContaAgenda Google = new(ProvedorAgenda.Google, "g@x.com", "#4C7DF0");
    private static readonly ContaAgenda Microsoft = new(ProvedorAgenda.Microsoft, "m@x.com", "#2BA9E0");
    private static readonly DateTimeOffset Agora = new(2026, 10, 28, 14, 0, 0, TimeSpan.FromHours(-3));

    private static Compromisso C(string id, ContaAgenda conta, double horasDepois) =>
        new(id, conta, id, Agora.AddHours(horasDepois), Agora.AddHours(horasDepois + 1), false, null, null, null, false, false);

    private sealed class FonteFalsa(ContaAgenda conta) : IFonteAgenda
    {
        public ContaAgenda Conta { get; } = conta;
        public Func<IReadOnlyList<Compromisso>> Resposta { get; set; } = () => [];
        public (DateTimeOffset De, DateTimeOffset Ate)? Pedido { get; private set; }

        public Task<IReadOnlyList<Compromisso>> Ler(DateTimeOffset de, DateTimeOffset ate, CancellationToken cancelar)
        {
            Pedido = (de, ate);
            return Task.FromResult(Resposta());
        }
    }

    private static ServicoAgenda Criar(params IFonteAgenda[] fontes)
    {
        var s = new ServicoAgenda(() => Agora, Brasilia);
        s.DefinirFontes(fontes);
        return s;
    }

    [Fact]
    public async Task Junta_as_fontes()
    {
        var g = new FonteFalsa(Google) { Resposta = () => [C("g2", Google, 2)] };
        var m = new FonteFalsa(Microsoft) { Resposta = () => [C("m1", Microsoft, 1)] };
        var s = Criar(g, m);
        await s.Atualizar(CancellationToken.None);
        Assert.Equal(["m1", "g2"], s.Compromissos.Select(c => c.Id));
        Assert.Equal(EstadoConta.Ok, s.Contas[Google].Estado);
        Assert.Equal(Agora, s.Contas[Microsoft].AtualizadoEm);
    }

    [Fact]
    public async Task Fonte_com_falha_mantem_a_ultima_leitura()
    {
        var g = new FonteFalsa(Google) { Resposta = () => [C("g1", Google, 1)] };
        var s = Criar(g);
        await s.Atualizar(CancellationToken.None);
        g.Resposta = () => throw new HttpRequestException("fora do ar");
        await s.Atualizar(CancellationToken.None);
        Assert.Equal(["g1"], s.Compromissos.Select(c => c.Id));
        Assert.Equal(EstadoConta.SemConexao, s.Contas[Google].Estado);
        Assert.Equal(Agora, s.Contas[Google].AtualizadoEm);
    }

    [Fact]
    public async Task Conta_que_precisa_entrar_mantem_os_dados_e_avisa()
    {
        var g = new FonteFalsa(Google) { Resposta = () => [C("g1", Google, 1)] };
        var m = new FonteFalsa(Microsoft) { Resposta = () => [C("m1", Microsoft, 2)] };
        var s = Criar(g, m);
        await s.Atualizar(CancellationToken.None);
        g.Resposta = () => throw new ContaPrecisaEntrarException();
        await s.Atualizar(CancellationToken.None);
        Assert.Equal(["g1", "m1"], s.Compromissos.Select(c => c.Id));
        Assert.Equal(EstadoConta.PrecisaEntrar, s.Contas[Google].Estado);
        Assert.Equal(EstadoConta.Ok, s.Contas[Microsoft].Estado);
    }

    [Fact]
    public async Task Fonte_removida_some_da_lista()
    {
        var g = new FonteFalsa(Google) { Resposta = () => [C("g1", Google, 1)] };
        var m = new FonteFalsa(Microsoft) { Resposta = () => [C("m1", Microsoft, 2)] };
        var s = Criar(g, m);
        await s.Atualizar(CancellationToken.None);
        s.DefinirFontes([m]);
        Assert.Equal(["m1"], s.Compromissos.Select(c => c.Id));
        Assert.False(s.Contas.ContainsKey(Google));
    }

    [Fact]
    public async Task Avisos_nao_repetem_entre_chamadas()
    {
        var g = new FonteFalsa(Google) { Resposta = () => [C("g1", Google, 0.1)] };
        var s = Criar(g);
        await s.Atualizar(CancellationToken.None);
        Assert.Single(s.AvisosParaMostrar(TimeSpan.FromMinutes(10)));
        await s.Atualizar(CancellationToken.None);
        Assert.Empty(s.AvisosParaMostrar(TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task Le_o_intervalo_de_tres_meses()
    {
        var g = new FonteFalsa(Google);
        await Criar(g).Atualizar(CancellationToken.None);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(-3)), g.Pedido!.Value.De);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(-3)), g.Pedido!.Value.Ate);
    }

    [Fact]
    public async Task Mudou_so_quando_muda()
    {
        var g = new FonteFalsa(Google) { Resposta = () => [C("g1", Google, 1)] };
        var s = Criar(g);
        int vezes = 0;
        s.Mudou += () => vezes++;
        await s.Atualizar(CancellationToken.None);
        await s.Atualizar(CancellationToken.None);
        Assert.Equal(1, vezes);
    }
}

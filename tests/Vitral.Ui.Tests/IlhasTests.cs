using Vitral.Core.Perfis;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class IlhasTests
{
    private readonly RelogioFalso _relogio = new();
    private readonly VolumeFalso _volume = new();
    private readonly RedeFalsa _rede = new();
    private readonly BateriaFalsa _bateria = new();
    private readonly DesempenhoFalso _desempenho = new();

    private IlhaStatusViewModel Status(IlhasBarra? ilhas = null) =>
        new(_desempenho, _volume, _rede, _bateria, _relogio, ilhas ?? IlhasBarra.Todas);

    private static EstadoMidia Tocando(string? titulo, string? artista = null) => new(titulo, artista, "Spotify", true, null);

    [Fact]
    public void Midia_some_sem_sessao()
    {
        var vm = new IlhaMidiaViewModel(new MidiaFalsa());
        Assert.False(vm.Visivel);
    }

    [Fact]
    public void Midia_aparece_e_some_com_o_evento()
    {
        var fonte = new MidiaFalsa();
        var vm = new IlhaMidiaViewModel(fonte);
        fonte.Atual = Tocando("Música");
        Assert.True(vm.Visivel);
        Assert.True(vm.Tocando);
        Assert.Equal("Música", vm.Titulo);
        fonte.Atual = null;
        Assert.False(vm.Visivel);
    }

    [Fact]
    public void Titulo_longo_truncado()
    {
        var fonte = new MidiaFalsa { Atual = Tocando(new string('a', 40)) };
        var vm = new IlhaMidiaViewModel(fonte);
        Assert.Equal(IlhaMidiaViewModel.LimiteTitulo, vm.Titulo.Length);
        Assert.EndsWith("…", vm.Titulo);
    }

    [Fact]
    public void Titulo_vazio_usa_nome_do_app()
    {
        var fonte = new MidiaFalsa { Atual = Tocando(null) };
        var vm = new IlhaMidiaViewModel(fonte);
        Assert.Equal("Spotify", vm.Titulo);
        fonte.Atual = Tocando("");
        Assert.Equal("Spotify", vm.Titulo);
        fonte.Atual = Tocando("Título", "Artista");
        Assert.Equal("Título — Artista", vm.Titulo);
    }

    [Fact]
    public void Comandos_de_midia_chegam_na_fonte()
    {
        var fonte = new MidiaFalsa { Atual = Tocando("x") };
        var vm = new IlhaMidiaViewModel(fonte);
        vm.AnteriorCommand.Execute(null);
        vm.TocarPausarCommand.Execute(null);
        vm.ProximaCommand.Execute(null);
        vm.TrazerAppCommand.Execute(null);
        Assert.Equal(["anterior", "tocarPausar", "proxima", "trazerApp"], fonte.Comandos);
    }

    [Fact]
    public void Relogio_em_ptBR()
    {
        var agora = new AgoraFalso(new DateTime(2026, 10, 23, 9, 5, 0));
        using var vm = new IlhaRelogioViewModel(agora, _relogio);
        Assert.Equal("09:05", vm.Hora);
        Assert.Equal("sex, 23 out", vm.Data);

        agora.Agora = new DateTime(2026, 5, 4, 21, 30, 0);
        _relogio.Tique();
        Assert.Equal("21:30", vm.Hora);
        Assert.Equal("seg, 4 mai", vm.Data);
    }

    [Fact]
    public void Cpu_sem_leitura_mostra_traco()
    {
        using var vm = Status();
        Assert.Equal("—", vm.Cpu);
        Assert.Equal(0, vm.CpuFracao);
        Assert.Equal("—", vm.Memoria);

        _desempenho.Proximo = new Desempenho(0.37, 0.62);
        _relogio.Tique();
        Assert.Equal("37%", vm.Cpu);
        Assert.Equal(0.37, vm.CpuFracao);
        Assert.Equal("62%", vm.Memoria);
    }

    [Fact]
    public void Desempenho_desligado_esconde()
    {
        using var vm = Status(IlhasBarra.Todas with { Desempenho = false });
        Assert.False(vm.MostrarDesempenho);
    }

    [Fact]
    public void Volume_null_mostra_traco_e_nao_quebra()
    {
        _volume.Volume = null;
        _volume.Mudo = null;
        using var vm = Status();
        Assert.Equal("—", vm.Volume);
        vm.Rolar(1);
        vm.AlternarMudoCommand.Execute(null);
        Assert.Empty(_volume.Definidos);
    }

    [Theory]
    [InlineData(50, 1, 52)]
    [InlineData(99, 1, 100)]
    [InlineData(1, -1, 0)]
    [InlineData(50, -3, 44)]
    public void Roda_do_volume_soma_2_e_limita(int inicial, int passos, int esperado)
    {
        _volume.Volume = inicial;
        using var vm = Status();
        vm.Rolar(passos);
        Assert.Equal([esperado], _volume.Definidos);
        Assert.Equal($"{esperado}", vm.Volume);
    }

    [Fact]
    public void Clique_no_volume_alterna_o_mudo()
    {
        using var vm = Status();
        vm.AlternarMudoCommand.Execute(null);
        Assert.Equal(1, _volume.MudoAlternado);
        Assert.True(vm.Mudo);
    }

    [Fact]
    public void Bateria_so_com_bateria()
    {
        using var vm = Status();
        Assert.False(vm.BateriaVisivel);
        _bateria.Atual = new EstadoBateria(81, false);
        _bateria.Disparar();
        Assert.True(vm.BateriaVisivel);
        Assert.Equal("81%", vm.Bateria);
    }

    // O ícone acompanha o nível (um desenho por décimo, Segoe Fluent Icons) e mostra o raio ao carregar.
    [Theory]
    [InlineData(15, false, "")]
    [InlineData(0, false, "")]
    [InlineData(94, false, "")]
    [InlineData(100, false, "")]
    [InlineData(15, true, "")]
    [InlineData(100, true, "")]
    public void Icone_da_bateria_acompanha_o_nivel(int porcentagem, bool carregando, string icone)
    {
        using var vm = Status();
        _bateria.Atual = new EstadoBateria(porcentagem, carregando);
        _bateria.Disparar();
        Assert.Equal(icone, vm.IconeBateria);
    }

    [Fact]
    public void Rede_acompanha_a_fonte()
    {
        using var vm = Status();
        Assert.Equal(TipoRede.Wifi, vm.Rede);
        _rede.Atual = null;
        _rede.Disparar();
        Assert.Null(vm.Rede);
    }

    [Fact]
    public void Dispose_para_o_relogio()
    {
        var vm = Status();
        vm.Dispose();
        Assert.True(_relogio.Liberado);
    }
}

using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PainelMidiaTests
{
    private static readonly DateTime Lida = new(2026, 10, 25, 10, 0, 0);
    private readonly MidiaFalsa _midia = new();
    private readonly RelogioFalso _relogio = new();
    private readonly AgoraFalso _agora = new(Lida);

    private PainelMidiaViewModel Criar() => new(_midia, _relogio, _agora);

    private static EstadoMidia Estado(bool tocando, TimeSpan posicao, TimeSpan duracao, string? artista = "The Weeknd") =>
        new("Blinding Lights", artista, "Spotify", tocando, null, new LinhaDoTempo(posicao, duracao, Lida));

    private static TimeSpan M(int min, int s) => new(0, min, s);

    [Fact]
    public void Progresso_e_tempos()
    {
        _midia.Atual = Estado(false, M(1, 16), M(3, 20));
        using var vm = Criar();
        Assert.Equal("Blinding Lights", vm.Titulo);
        Assert.Equal("The Weeknd · Spotify", vm.Subtitulo);
        Assert.True(vm.MostrarProgresso);
        Assert.Equal("1:16", vm.Decorrido);
        Assert.Equal("3:20", vm.Total);
        Assert.Equal(0.38, vm.Fracao, 2);
    }

    [Fact]
    public void Tique_avanca_enquanto_toca()
    {
        _midia.Atual = Estado(true, M(1, 16), M(3, 20));
        using var vm = Criar();
        _agora.Agora = Lida.AddSeconds(10);
        _relogio.Tique();
        Assert.Equal("1:26", vm.Decorrido);
    }

    [Fact]
    public void Pausado_nao_avanca()
    {
        _midia.Atual = Estado(false, M(1, 16), M(3, 20));
        using var vm = Criar();
        _agora.Agora = Lida.AddSeconds(10);
        _relogio.Tique();
        Assert.Equal("1:16", vm.Decorrido);
    }

    [Fact]
    public void Posicao_alem_do_fim_fica_no_fim()
    {
        _midia.Atual = Estado(true, M(3, 15), M(3, 20));
        using var vm = Criar();
        _agora.Agora = Lida.AddMinutes(5);
        _relogio.Tique();
        Assert.Equal("3:20", vm.Decorrido);
        Assert.Equal(1, vm.Fracao);
    }

    [Fact]
    public void Sem_duracao_esconde_o_progresso()
    {
        _midia.Atual = Estado(true, TimeSpan.Zero, TimeSpan.Zero);
        using var vm = Criar();
        Assert.False(vm.MostrarProgresso);
        _midia.Atual = new EstadoMidia("Rádio", null, "Edge", true, null);
        Assert.False(vm.MostrarProgresso);
    }

    [Fact]
    public void Mais_de_uma_hora()
    {
        _midia.Atual = Estado(false, new TimeSpan(1, 2, 5), new TimeSpan(2, 0, 0));
        using var vm = Criar();
        Assert.Equal("1:02:05", vm.Decorrido);
        Assert.Equal("2:00:00", vm.Total);
    }

    [Fact]
    public void Subtitulo_sem_artista()
    {
        _midia.Atual = Estado(false, M(0, 0), M(3, 0), artista: null);
        using var vm = Criar();
        Assert.Equal("Spotify", vm.Subtitulo);
    }

    [Fact]
    public void Abrir_app_chega_na_fonte()
    {
        _midia.Atual = Estado(true, M(0, 0), M(3, 0));
        using var vm = Criar();
        vm.AbrirAppCommand.Execute(null);
        vm.TocarPausarCommand.Execute(null);
        Assert.Equal(["trazerApp", "tocarPausar"], _midia.Comandos);
    }
}

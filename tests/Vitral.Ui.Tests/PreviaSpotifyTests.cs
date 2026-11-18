using Vitral.Core.Barra;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

internal sealed class GuardaFalsa : IGuardaUltimaMusica
{
    public UltimaMusica? Guardada { get; set; }
    public int Gravacoes { get; private set; }
    public UltimaMusica? Ler() => Guardada;
    public void Gravar(UltimaMusica musica) { Guardada = musica; Gravacoes++; }
}

internal sealed class AbridorFalso : IAbridorSpotify
{
    public int Aberturas { get; private set; }
    public int Tocadas { get; private set; }
    public TaskCompletionSource<bool> Resultado { get; set; } = new();
    public void Abrir() => Aberturas++;
    public Task<bool> AbrirETocarAsync() { Tocadas++; return Resultado.Task; }
}

public sealed class PreviaSpotifyTests
{
    private readonly MidiaFalsa _real = new();
    private readonly GuardaFalsa _guarda = new();
    private readonly AbridorFalso _abridor = new();

    private FonteMidiaComPrevia Criar() => new(_real, _guarda, _abridor);

    private static EstadoMidia Spotify(string titulo = "Blinding Lights", bool tocando = true, byte[]? capa = null) =>
        new(titulo, "The Weeknd", "Spotify", tocando, capa);

    [Fact]
    public void Com_sessao_repassa_o_estado_real()
    {
        var real = new EstadoMidia("Vídeo", null, "Msedge", true, null);
        _real.Atual = real;
        using var fonte = Criar();
        Assert.Same(real, fonte.Atual);
        Assert.False(fonte.Atual!.Previa);
    }

    [Fact]
    public void Sem_sessao_e_sem_guardada_nao_mostra_nada()
    {
        using var fonte = Criar();
        Assert.Null(fonte.Atual);
    }

    [Fact]
    public void Spotify_tocando_grava_e_ao_fechar_vira_previa()
    {
        using var fonte = Criar();
        _real.Atual = Spotify(capa: [1, 2]);
        Assert.Equal("Blinding Lights", _guarda.Guardada?.Titulo);
        _real.Atual = null;
        var previa = fonte.Atual;
        Assert.NotNull(previa);
        Assert.True(previa.Previa);
        Assert.False(previa.Tocando);
        Assert.Equal("Blinding Lights", previa.Titulo);
        Assert.Equal("The Weeknd", previa.Artista);
        Assert.Equal("Spotify", previa.NomeApp);
        Assert.Equal([1, 2], previa.Capa);
        Assert.Null(previa.Tempo);
    }

    [Fact]
    public void Guardada_de_antes_aparece_ao_abrir()
    {
        _guarda.Guardada = new UltimaMusica("Antiga", "X", null, false);
        using var fonte = Criar();
        Assert.Equal("Antiga", fonte.Atual?.Titulo);
        Assert.True(fonte.Atual!.Previa);
    }

    [Fact]
    public void Outro_player_nao_grava()
    {
        using var fonte = Criar();
        _real.Atual = new EstadoMidia("Vídeo", null, "Msedge", true, null);
        Assert.Null(_guarda.Guardada);
    }

    [Fact]
    public void Nao_regrava_a_mesma_musica()
    {
        using var fonte = Criar();
        _real.Atual = Spotify(capa: [1]);
        _real.Atual = Spotify(capa: [1]); // a linha do tempo mudou: o mesmo estado chega de novo
        _real.Atual = Spotify(tocando: false, capa: [1]);
        Assert.Equal(1, _guarda.Gravacoes);
    }

    [Fact]
    public void Capa_que_chega_depois_e_guardada_e_capa_nula_da_mesma_musica_mantem_a_velha()
    {
        using var fonte = Criar();
        _real.Atual = Spotify();
        _real.Atual = Spotify(capa: [7]);
        Assert.Equal([7], _guarda.Guardada!.Capa);
        _real.Atual = Spotify(capa: null);
        Assert.Equal([7], _guarda.Guardada!.Capa);
        _real.Atual = Spotify("Outra", capa: null);
        Assert.Null(_guarda.Guardada!.Capa);
    }

    [Fact]
    public void X_esconde_ate_o_spotify_tocar()
    {
        using var fonte = Criar();
        _real.Atual = Spotify();
        _real.Atual = null;
        int avisos = 0;
        fonte.Mudou += () => avisos++;
        fonte.EsconderPrevia();
        Assert.Null(fonte.Atual);
        Assert.True(avisos > 0);
        Assert.True(_guarda.Guardada!.Escondida);

        _real.Atual = Spotify(tocando: false); // aberto e pausado não desfaz o ×
        _real.Atual = null;
        Assert.Null(fonte.Atual);

        _real.Atual = Spotify(tocando: true);
        _real.Atual = null;
        Assert.True(fonte.Atual?.Previa);
        Assert.False(_guarda.Guardada!.Escondida);
    }

    [Fact]
    public void X_guardado_continua_escondido_ao_abrir()
    {
        _guarda.Guardada = new UltimaMusica("Antiga", "X", null, Escondida: true);
        using var fonte = Criar();
        Assert.Null(fonte.Atual);
    }

    [Fact]
    public void Play_na_previa_abre_e_toca_e_mostra_tocando_enquanto_abre()
    {
        _guarda.Guardada = new UltimaMusica("Antiga", "X", null, false);
        using var fonte = Criar();
        fonte.TocarPausar();
        Assert.Equal(1, _abridor.Tocadas);
        Assert.True(fonte.Atual!.Tocando);
        fonte.TocarPausar(); // segundo clique enquanto abre não abre de novo
        Assert.Equal(1, _abridor.Tocadas);
        Assert.Empty(_real.Comandos);
    }

    [Fact]
    public void Play_que_falha_volta_ao_play()
    {
        _guarda.Guardada = new UltimaMusica("Antiga", "X", null, false);
        using var fonte = Criar();
        _abridor.Resultado.SetResult(false); // termina na hora: a continuação roda dentro do TocarPausar
        fonte.TocarPausar();
        Assert.Equal(1, _abridor.Tocadas);
        Assert.False(fonte.Atual!.Tocando);
    }

    [Fact]
    public void Sessao_que_aparece_encerra_a_abertura()
    {
        _guarda.Guardada = new UltimaMusica("Antiga", "X", null, false);
        using var fonte = Criar();
        fonte.TocarPausar();
        _real.Atual = Spotify("Antiga", tocando: false);
        _real.Atual = null; // fechou de novo antes de tocar
        Assert.False(fonte.Atual!.Tocando);
    }

    [Fact]
    public void Comandos_na_previa()
    {
        _guarda.Guardada = new UltimaMusica("Antiga", "X", null, false);
        using var fonte = Criar();
        fonte.Anterior();
        fonte.Proxima();
        fonte.TrazerApp();
        Assert.Empty(_real.Comandos);
        Assert.Equal(1, _abridor.Aberturas);
    }

    [Fact]
    public void Comandos_com_sessao_vao_para_a_fonte_real()
    {
        _real.Atual = Spotify();
        using var fonte = Criar();
        fonte.Anterior();
        fonte.TocarPausar();
        fonte.Proxima();
        fonte.TrazerApp();
        Assert.Equal(["anterior", "tocarPausar", "proxima", "trazerApp"], _real.Comandos);
        Assert.Equal(0, _abridor.Tocadas + _abridor.Aberturas);
    }

    [Fact]
    public void Dispose_solta_a_fonte_real()
    {
        var fonte = Criar();
        int avisos = 0;
        fonte.Mudou += () => avisos++;
        fonte.Dispose();
        _real.Atual = Spotify();
        Assert.Equal(0, avisos);
    }
}

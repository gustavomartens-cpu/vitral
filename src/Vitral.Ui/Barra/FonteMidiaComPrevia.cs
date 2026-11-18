using Vitral.Core.Barra;

namespace Vitral.Ui.Barra;

/// Abre o Spotify (spec da prévia do Spotify §2). O App implementa com o URI spotify: e os controles de mídia do Windows.
public interface IAbridorSpotify
{
    /// Só abre (o clique no título da prévia).
    void Abrir();

    /// Abre e manda tocar quando a sessão do Spotify aparecer; false se não tocou no prazo ou foi cancelado (o × ou
    /// um comando na sessão real). Termina na thread da interface.
    Task<bool> AbrirETocarAsync(CancellationToken cancelar);
}

/// A música do Windows com a prévia do Spotify (spec da prévia do Spotify): com sessão, repassa a fonte real (e guarda a
/// música quando ela vem do Spotify); sem sessão, mostra a última do Spotify com Previa = true, a não ser que o × a tenha
/// escondido. O × vale até o Spotify tocar de novo.
public sealed class FonteMidiaComPrevia : IFonteMidia, IDisposable
{
    public const string NomeSpotify = "Spotify";

    private readonly IFonteMidia _real;
    private readonly IGuardaUltimaMusica _guarda;
    private readonly IAbridorSpotify _abridor;
    private UltimaMusica? _ultima;
    private bool _abrindo;
    private bool _realPublicou; // antes da primeira leitura do Windows, null não quer dizer "nada tocando"
    private CancellationTokenSource? _abertura;
    private EstadoMidia? _previa; // a mesma instância (e a mesma capa) enquanto nada muda: o notch lê a capa uma vez

    public EstadoMidia? Atual { get; private set; }
    public event Action? Mudou;

    public FonteMidiaComPrevia(IFonteMidia real, IGuardaUltimaMusica guarda, IAbridorSpotify abridor)
    {
        _real = real;
        _guarda = guarda;
        _abridor = abridor;
        _ultima = guarda.Ler();
        _real.Mudou += AoMudarReal;
        Guardar(_real.Atual);
        Recalcular();
    }

    private bool EmPrevia => Atual?.Previa == true;

    private void AoMudarReal()
    {
        var m = _real.Atual;
        _realPublicou = true;
        Guardar(m);
        if (m is not null) _abrindo = false; // a sessão apareceu: dali em diante vale a real
        Publicar();
    }

    /// Guarda a música do Spotify quando ela muda; tocar desfaz o ×.
    private void Guardar(EstadoMidia? m)
    {
        if (m is not { NomeApp: NomeSpotify } || string.IsNullOrWhiteSpace(m.Titulo) || EhAnuncio(m)) return;
        bool mesma = _ultima is { } u && u.Titulo == m.Titulo && u.Artista == m.Artista;
        var capa = m.Capa is { Length: > 0 } ? m.Capa : mesma ? _ultima!.Capa : null; // a capa costuma chegar depois
        bool escondida = _ultima?.Escondida == true && !m.Tocando;
        if (mesma && _ultima!.Escondida == escondida && MesmaCapa(_ultima.Capa, capa)) return;
        _ultima = new UltimaMusica(m.Titulo, m.Artista, capa, escondida);
        _guarda.Gravar(_ultima);
    }

    /// O Spotify Free publica os anúncios como faixa ("Advertisement", "Spotify"), em geral sem artista.
    private static bool EhAnuncio(EstadoMidia m) =>
        string.IsNullOrWhiteSpace(m.Artista) || m.Titulo is "Advertisement" or "Anúncio" or NomeSpotify;

    private static bool MesmaCapa(byte[]? a, byte[]? b) =>
        ReferenceEquals(a, b) || (a is not null && b is not null && a.AsSpan().SequenceEqual(b));

    private void Recalcular()
    {
        if (_real.Atual is { } real)
        {
            Atual = real;
            return;
        }
        if (!_realPublicou || _ultima is not { Escondida: false } u)
        {
            Atual = _previa = null;
            return;
        }
        if (_previa is null || _previa.Titulo != u.Titulo || _previa.Artista != u.Artista || !ReferenceEquals(_previa.Capa, u.Capa)
            || _previa.Tocando != _abrindo)
            _previa = new EstadoMidia(u.Titulo, u.Artista, NomeSpotify, _abrindo, u.Capa, Tempo: null, Previa: true);
        Atual = _previa;
    }

    private void Publicar()
    {
        Recalcular();
        Mudou?.Invoke();
    }

    public void Anterior()
    {
        if (EmPrevia) return;
        CancelarAbertura();
        _real.Anterior();
    }

    public void Proxima()
    {
        if (EmPrevia) return;
        CancelarAbertura();
        _real.Proxima();
    }

    public void TrazerApp()
    {
        if (EmPrevia) _abridor.Abrir();
        else _real.TrazerApp();
    }

    public void TocarPausar()
    {
        if (!EmPrevia)
        {
            CancelarAbertura(); // o usuário mandou na sessão real: a abertura não insiste no play
            _real.TocarPausar();
            return;
        }
        if (_abrindo) return;
        _ = AbrirETocarAsync();
    }

    private async Task AbrirETocarAsync()
    {
        CancelarAbertura();
        var abertura = _abertura = new CancellationTokenSource();
        _abrindo = true;
        Publicar();
        bool tocou;
        try { tocou = await _abridor.AbrirETocarAsync(abertura.Token); }
        catch (Exception) { tocou = false; } // contrato das fontes: nunca lançam
        if (!ReferenceEquals(abertura, _abertura)) return; // cancelada (o ×, um comando, o Dispose)
        _abertura = null;
        abertura.Dispose();
        if (!_abrindo) return; // a sessão real já apareceu
        _abrindo = false; // tocou: a sessão real assume na publicação seguinte; não tocou: volta o play
        if (!tocou) Publicar();
    }

    private void CancelarAbertura()
    {
        if (_abertura is not { } a) return;
        _abertura = null;
        a.Cancel();
        a.Dispose();
    }

    public void EsconderPrevia()
    {
        if (_ultima is null || _ultima.Escondida) return;
        _ultima = _ultima with { Escondida = true };
        _guarda.Gravar(_ultima);
        CancelarAbertura(); // o × no meio da abertura: o Spotify não começa a tocar contra ele
        _abrindo = false;
        Publicar();
    }

    public void Dispose()
    {
        CancelarAbertura();
        _real.Mudou -= AoMudarReal;
    }
}

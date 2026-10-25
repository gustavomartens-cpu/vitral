using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui.Barra;

/// Mídia expandida (spec v2 §3.3): capa, título, "artista · app", progresso e controles.
/// A posição é estimada a cada segundo enquanto toca (o Windows só avisa de tempos em tempos), limitada à duração.
public sealed partial class PainelMidiaViewModel : ObservableObject, IDisposable
{
    private readonly IFonteMidia _fonte;
    private readonly IRelogioDoSistema _agora;
    private readonly IDisposable _tique;

    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _subtitulo = "";
    [ObservableProperty] private byte[]? _capa;
    [ObservableProperty] private bool _tocando;
    [ObservableProperty] private bool _mostrarProgresso;
    [ObservableProperty] private double _fracao;
    [ObservableProperty] private string _decorrido = "";
    [ObservableProperty] private string _total = "";

    public PainelMidiaViewModel(IFonteMidia fonte, IRelogio relogio, IRelogioDoSistema agora)
    {
        _fonte = fonte;
        _agora = agora;
        _fonte.Mudou += Ler;
        Ler();
        _tique = relogio.ACadaSegundo(AtualizarProgresso);
    }

    private void Ler()
    {
        var m = _fonte.Atual;
        Titulo = m is null ? "" : string.IsNullOrWhiteSpace(m.Titulo) ? m.NomeApp : m.Titulo;
        Subtitulo = m is null ? "" : string.IsNullOrWhiteSpace(m.Artista) ? m.NomeApp : $"{m.Artista} · {m.NomeApp}";
        Capa = m?.Capa;
        Tocando = m?.Tocando ?? false;
        AtualizarProgresso();
    }

    private void AtualizarProgresso()
    {
        var m = _fonte.Atual;
        if (m?.Tempo is not { Duracao.Ticks: > 0 } t)
        {
            MostrarProgresso = false;
            return;
        }
        var posicao = m.Tocando ? t.Posicao + (_agora.Agora - t.LidaEm) : t.Posicao;
        if (posicao < TimeSpan.Zero) posicao = TimeSpan.Zero;
        if (posicao > t.Duracao) posicao = t.Duracao;
        MostrarProgresso = true;
        Fracao = posicao / t.Duracao;
        Decorrido = Formatar(posicao);
        Total = Formatar(t.Duracao);
    }

    private static string Formatar(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";

    [RelayCommand] private void Anterior() => _fonte.Anterior();
    [RelayCommand] private void TocarPausar() => _fonte.TocarPausar();
    [RelayCommand] private void Proxima() => _fonte.Proxima();
    [RelayCommand] private void AbrirApp() => _fonte.TrazerApp();

    public void Dispose()
    {
        _tique.Dispose();
        _fonte.Mudou -= Ler;
    }
}

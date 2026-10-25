using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui.Barra;

/// Ilha de mídia: some quando nada toca; título "Título — Artista" (ou o nome do app), truncado com "…".
public sealed partial class IlhaMidiaViewModel : ObservableObject, IDisposable
{
    public const int LimiteTitulo = 32;

    private readonly IFonteMidia _fonte;

    [ObservableProperty] private bool _visivel;
    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private bool _tocando;
    [ObservableProperty] private byte[]? _capa;

    public IlhaMidiaViewModel(IFonteMidia fonte)
    {
        _fonte = fonte;
        _fonte.Mudou += Atualizar;
        Atualizar();
    }

    private void Atualizar()
    {
        var m = _fonte.Atual;
        Visivel = m is not null;
        Tocando = m?.Tocando ?? false;
        Capa = m?.Capa;
        Titulo = m is null ? "" : Truncar(Montar(m));
    }

    private static string Montar(EstadoMidia m)
    {
        if (string.IsNullOrWhiteSpace(m.Titulo)) return m.NomeApp;
        return string.IsNullOrWhiteSpace(m.Artista) ? m.Titulo : $"{m.Titulo} — {m.Artista}";
    }

    private static string Truncar(string s) => s.Length <= LimiteTitulo ? s : s[..(LimiteTitulo - 1)] + "…";

    [RelayCommand] private void Anterior() => _fonte.Anterior();
    [RelayCommand] private void TocarPausar() => _fonte.TocarPausar();
    [RelayCommand] private void Proxima() => _fonte.Proxima();
    [RelayCommand] private void TrazerApp() => _fonte.TrazerApp();

    public void Dispose() => _fonte.Mudou -= Atualizar;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui.Barra;

/// Painel de volume (spec v2 §3.3): controle deslizante, mudo e troca da saída.
/// O valor que chega da fonte não volta para ela como "definir" (sem eco ao arrastar).
public sealed partial class PainelVolumeViewModel : ObservableObject, IDisposable
{
    private readonly IFonteVolume _fonteVolume;
    private readonly IFonteDispositivosAudio _fonteDispositivos;
    private bool _lendo;

    [ObservableProperty] private int _volume;
    [ObservableProperty] private bool _mudo;
    [ObservableProperty] private bool _disponivel;
    [ObservableProperty] private bool _mostrarSaidas;
    [ObservableProperty] private bool _microfoneMudo;
    [ObservableProperty] private bool _mostrarMicrofone;
    [ObservableProperty] private bool _mostrarEntradas;

    public ObservableCollection<DispositivoAudio> Saidas { get; } = [];

    /// Os microfones (spec dos controles §4.5).
    public ObservableCollection<DispositivoAudio> Entradas { get; } = [];

    private readonly IFonteMicrofone? _microfone;

    public PainelVolumeViewModel(IFonteVolume volume, IFonteDispositivosAudio dispositivos, IFonteMicrofone? microfone = null)
    {
        _fonteVolume = volume;
        _fonteDispositivos = dispositivos;
        _microfone = microfone;
        if (_microfone is not null) _microfone.Mudou += LerMicrofone;
        LerMicrofone();
        _fonteVolume.Mudou += LerVolume;
        _fonteDispositivos.Mudou += LerSaidas;
        LerVolume();
        LerSaidas();
    }

    partial void OnVolumeChanged(int value)
    {
        if (!_lendo) _fonteVolume.Definir(Math.Clamp(value, 0, 100));
    }

    [RelayCommand]
    private void AlternarMudo()
    {
        if (_fonteVolume.Mudo is null) return;
        _fonteVolume.AlternarMudo();
    }

    [RelayCommand]
    private void Escolher(DispositivoAudio? dispositivo)
    {
        if (dispositivo is null || dispositivo.Padrao || !_fonteDispositivos.PodeTrocar) return;
        _fonteDispositivos.TornarPadrao(dispositivo.Id);
    }

    private void LerVolume()
    {
        _lendo = true;
        try
        {
            Disponivel = _fonteVolume.Volume is not null;
            Volume = _fonteVolume.Volume ?? 0;
            Mudo = _fonteVolume.Mudo ?? false;
        }
        finally { _lendo = false; }
    }

    private void LerSaidas()
    {
        Saidas.Clear();
        foreach (var s in _fonteDispositivos.Saidas) Saidas.Add(s);
        MostrarSaidas = _fonteDispositivos.PodeTrocar && Saidas.Count > 0;
        Entradas.Clear();
        foreach (var e in _fonteDispositivos.Entradas) Entradas.Add(e);
        MostrarEntradas = _fonteDispositivos.PodeTrocar && Entradas.Count > 0; // sem a troca, o clique não faria nada
    }

    private void LerMicrofone()
    {
        MostrarMicrofone = _microfone?.Mudo is not null;
        MicrofoneMudo = _microfone?.Mudo ?? false;
    }

    [RelayCommand]
    private void AlternarMicrofone() => _microfone?.AlternarMudo();

    [RelayCommand]
    private void EscolherEntrada(DispositivoAudio? entrada)
    {
        if (entrada is null || entrada.Padrao || !_fonteDispositivos.PodeTrocar) return;
        _fonteDispositivos.TornarEntradaPadrao(entrada.Id);
    }

    public void Dispose()
    {
        _fonteVolume.Mudou -= LerVolume;
        _fonteDispositivos.Mudou -= LerSaidas;
        if (_microfone is not null) _microfone.Mudou -= LerMicrofone;
    }
}

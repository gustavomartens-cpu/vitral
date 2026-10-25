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

    public ObservableCollection<DispositivoAudio> Saidas { get; } = [];

    public PainelVolumeViewModel(IFonteVolume volume, IFonteDispositivosAudio dispositivos)
    {
        _fonteVolume = volume;
        _fonteDispositivos = dispositivos;
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
    }

    public void Dispose()
    {
        _fonteVolume.Mudou -= LerVolume;
        _fonteDispositivos.Mudou -= LerSaidas;
    }
}

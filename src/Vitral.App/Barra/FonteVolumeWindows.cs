using System.Runtime.InteropServices;
using System.Windows.Threading;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Volume do dispositivo de saída padrão pelo Core Audio (IAudioEndpointVolume). Acompanha a troca do dispositivo
/// padrão (fone Bluetooth, monitor HDMI...). Falha vira null.
internal sealed class FonteVolumeWindows : IFonteVolume, IDisposable
{
    private const int Saida = CoreAudio.Saida;
    private const int Multimidia = CoreAudio.Multimidia;

    private readonly Dispatcher _ui;
    private readonly IMMDeviceEnumerator? _enumerador;
    private readonly AvisoDeDispositivo? _avisoDispositivo;
    private IAudioEndpointVolume? _endpoint;
    private Retorno? _retorno;

    public int? Volume { get; private set; }
    public bool? Mudo { get; private set; }
    public event Action? Mudou;

    public FonteVolumeWindows(Dispatcher ui)
    {
        _ui = ui;
        try
        {
            _enumerador = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            _avisoDispositivo = new AvisoDeDispositivo(this);
            _enumerador.RegisterEndpointNotificationCallback(_avisoDispositivo);
        }
        catch (Exception e) { LogBarra.Erro("Volume.Iniciar", e); }
        Conectar();
    }

    /// (Re)liga ao dispositivo padrão atual; solta o anterior.
    private void Conectar()
    {
        Desconectar();
        try
        {
            if (_enumerador is null) return;
            Marshal.ThrowExceptionForHR(_enumerador.GetDefaultAudioEndpoint(Saida, Multimidia, out var dispositivo));
            try
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(dispositivo.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out object obj));
                _endpoint = (IAudioEndpointVolume)obj;
            }
            finally { Marshal.ReleaseComObject(dispositivo); }
            _retorno = new Retorno(this);
            Marshal.ThrowExceptionForHR(_endpoint.RegisterControlChangeNotify(_retorno));
        }
        catch (Exception e)
        {
            LogBarra.Erro("Volume.Conectar", e);
            Desconectar();
        }
        finally { Ler(); }
    }

    private void Desconectar()
    {
        if (_endpoint is null) return;
        try { if (_retorno is not null) _endpoint.UnregisterControlChangeNotify(_retorno); }
        catch (Exception e) { LogBarra.Erro("Volume.Desconectar", e); }
        Marshal.ReleaseComObject(_endpoint);
        _endpoint = null;
        _retorno = null;
    }

    private void Ler()
    {
        try
        {
            if (_endpoint is null) { Volume = null; Mudo = null; return; }
            Marshal.ThrowExceptionForHR(_endpoint.GetMasterVolumeLevelScalar(out float nivel));
            Marshal.ThrowExceptionForHR(_endpoint.GetMute(out bool mudo));
            Volume = (int)Math.Round(nivel * 100);
            Mudo = mudo;
        }
        catch (Exception e)
        {
            LogBarra.Erro("Volume.Ler", e);
            Volume = null;
            Mudo = null;
        }
    }

    private void AoNotificar() => _ui.BeginInvoke(() => { Ler(); Mudou?.Invoke(); });

    private void AoTrocarDispositivo() => _ui.BeginInvoke(() => { Conectar(); Mudou?.Invoke(); });

    public void Definir(int volume)
    {
        try
        {
            if (_endpoint is null) return;
            var contexto = Guid.Empty;
            Marshal.ThrowExceptionForHR(_endpoint.SetMasterVolumeLevelScalar(Math.Clamp(volume, 0, 100) / 100f, ref contexto));
        }
        catch (Exception e) { LogBarra.Erro("Volume.Definir", e); }
    }

    public void AlternarMudo()
    {
        try
        {
            if (_endpoint is null || Mudo is not { } mudo) return;
            var contexto = Guid.Empty;
            Marshal.ThrowExceptionForHR(_endpoint.SetMute(!mudo, ref contexto));
        }
        catch (Exception e) { LogBarra.Erro("Volume.Mudo", e); }
    }

    public void Dispose()
    {
        Desconectar();
        try
        {
            if (_enumerador is null) return;
            if (_avisoDispositivo is not null) _enumerador.UnregisterEndpointNotificationCallback(_avisoDispositivo);
            Marshal.ReleaseComObject(_enumerador);
        }
        catch (Exception e) { LogBarra.Erro("Volume.Dispose", e); }
    }

    private sealed class Retorno(FonteVolumeWindows dono) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr dados) { dono.AoNotificar(); return 0; }
    }

    private sealed class AvisoDeDispositivo(FonteVolumeWindows dono) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(IntPtr id, uint estado) => 0;
        public int OnDeviceAdded(IntPtr id) => 0;
        public int OnDeviceRemoved(IntPtr id) => 0;
        public int OnDefaultDeviceChanged(int fluxo, int papel, IntPtr id)
        {
            if (fluxo == Saida && papel == Multimidia) dono.AoTrocarDispositivo();
            return 0;
        }
        public int OnPropertyValueChanged(IntPtr id, IntPtr chave) => 0;
    }
}

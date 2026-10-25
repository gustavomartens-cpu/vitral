using System.Runtime.InteropServices;
using System.Windows.Threading;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Volume do dispositivo de saída padrão pelo Core Audio (IAudioEndpointVolume). Falha vira null.
internal sealed class FonteVolumeWindows : IFonteVolume, IDisposable
{
    private readonly Dispatcher _ui;
    private readonly IAudioEndpointVolume? _endpoint;
    private readonly Retorno? _retorno;

    public int? Volume { get; private set; }
    public bool? Mudo { get; private set; }
    public event Action? Mudou;

    public FonteVolumeWindows(Dispatcher ui)
    {
        _ui = ui;
        try
        {
            var enumerador = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerador.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var dispositivo));
            var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(dispositivo.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out object obj));
            _endpoint = (IAudioEndpointVolume)obj;
            _retorno = new Retorno(this);
            _endpoint.RegisterControlChangeNotify(_retorno);
            Ler();
        }
        catch (Exception e)
        {
            LogBarra.Erro("Volume.Iniciar", e);
            _endpoint = null;
        }
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

    public void Definir(int volume)
    {
        try
        {
            var contexto = Guid.Empty;
            _endpoint?.SetMasterVolumeLevelScalar(Math.Clamp(volume, 0, 100) / 100f, ref contexto);
        }
        catch (Exception e) { LogBarra.Erro("Volume.Definir", e); }
    }

    public void AlternarMudo()
    {
        try
        {
            if (_endpoint is null || Mudo is not { } mudo) return;
            var contexto = Guid.Empty;
            _endpoint.SetMute(!mudo, ref contexto);
        }
        catch (Exception e) { LogBarra.Erro("Volume.Mudo", e); }
    }

    public void Dispose()
    {
        try { if (_endpoint is not null && _retorno is not null) _endpoint.UnregisterControlChangeNotify(_retorno); }
        catch (Exception e) { LogBarra.Erro("Volume.Dispose", e); }
    }

    private sealed class Retorno(FonteVolumeWindows dono) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr dados) { dono.AoNotificar(); return 0; }
    }

    // ---- Core Audio (mmdeviceapi.h, endpointvolume.h) ----

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int fluxo, int estado, out IntPtr dispositivos);
        [PreserveSig] int GetDefaultAudioEndpoint(int fluxo, int papel, out IMMDevice dispositivo);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int contexto, IntPtr parametros, [MarshalAs(UnmanagedType.IUnknown)] out object interfaceAtivada);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr dados);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback retorno);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback retorno);
        [PreserveSig] int GetChannelCount(out uint canais);
        [PreserveSig] int SetMasterVolumeLevel(float nivelDb, ref Guid contexto);
        [PreserveSig] int SetMasterVolumeLevelScalar(float nivel, ref Guid contexto);
        [PreserveSig] int GetMasterVolumeLevel(out float nivelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float nivel);
        [PreserveSig] int SetChannelVolumeLevel(uint canal, float nivelDb, ref Guid contexto);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint canal, float nivel, ref Guid contexto);
        [PreserveSig] int GetChannelVolumeLevel(uint canal, out float nivelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint canal, out float nivel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mudo, ref Guid contexto);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mudo);
    }
}

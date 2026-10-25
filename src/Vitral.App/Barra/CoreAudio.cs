using System.Runtime.InteropServices;

namespace Vitral.App.Barra;

// Core Audio (mmdeviceapi.h, endpointvolume.h, propsys) e IPolicyConfig (interface interna do Windows, não
// documentada; a mesma que o EarTrumpet usa para trocar o dispositivo padrão).

internal static class CoreAudio
{
    public const int Saida = 0;              // eRender
    public const int Multimidia = 1;         // eMultimedia
    public const int DispositivoAtivo = 1;   // DEVICE_STATE_ACTIVE
    public const int SomenteLeitura = 0;     // STGM_READ
    public const int ClsctxAll = 23;

    /// PKEY_Device_FriendlyName
    public static readonly PropertyKey NomeAmigavel = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    [DllImport("ole32.dll")] public static extern int PropVariantClear(ref PropVariant valor);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PropertyKey(Guid Formato, int Id);

[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    [FieldOffset(0)] public ushort Tipo;
    [FieldOffset(8)] public IntPtr Ponteiro;
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumerator;

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int fluxo, int estado, out IMMDeviceCollection dispositivos);
    [PreserveSig] int GetDefaultAudioEndpoint(int fluxo, int papel, out IMMDevice dispositivo);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice dispositivo);
    [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient cliente);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient cliente);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint quantidade);
    [PreserveSig] int Item(uint indice, out IMMDevice dispositivo);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int contexto, IntPtr parametros, [MarshalAs(UnmanagedType.IUnknown)] out object interfaceAtivada);
    [PreserveSig] int OpenPropertyStore(int acesso, out IPropertyStore propriedades);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out int estado);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint quantidade);
    [PreserveSig] int GetAt(uint indice, out PropertyKey chave);
    [PreserveSig] int GetValue(ref PropertyKey chave, out PropVariant valor);
}

[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    [PreserveSig] int OnDeviceStateChanged(IntPtr id, uint estado);
    [PreserveSig] int OnDeviceAdded(IntPtr id);
    [PreserveSig] int OnDeviceRemoved(IntPtr id);
    [PreserveSig] int OnDefaultDeviceChanged(int fluxo, int papel, IntPtr id);
    [PreserveSig] int OnPropertyValueChanged(IntPtr id, IntPtr chave);
}

[ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    [PreserveSig] int OnNotify(IntPtr dados);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
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

[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClient;

/// Só SetDefaultEndpoint é usado; os anteriores existem para manter a ordem da vtable.
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
    [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
    [PreserveSig] int ResetDeviceFormat(IntPtr a);
    [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
    [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int GetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int SetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int papel);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visivel);
}

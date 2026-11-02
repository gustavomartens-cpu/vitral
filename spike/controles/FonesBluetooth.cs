using System.Runtime.InteropServices;

/// Fones e caixas Bluetooth pelo áudio do Windows (DESCARTÁVEL): o mesmo caminho do ToothTray. Cada saída de áudio
/// Bluetooth tem um filtro KS do driver BthA2DP/BthHFP; a propriedade KSPROPSETID_BtAudio (ONESHOT_RECONNECT=0,
/// ONESHOT_DISCONNECT=1) pede ao Windows para conectar ou desconectar o aparelho.
static class FonesBluetooth
{
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int fluxo, uint estados, out IMMDeviceCollection colecao);
        [PreserveSig] int GetDefaultAudioEndpoint(int fluxo, int papel, out IMMDevice dispositivo);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice dispositivo);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint n);
        [PreserveSig] int Item(uint i, out IMMDevice dispositivo);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint contexto, IntPtr parametros, [MarshalAs(UnmanagedType.IUnknown)] out object interfaceAtivada);
        [PreserveSig] int OpenPropertyStore(uint acesso, out IntPtr loja);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint estado);
    }

    [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceTopology
    {
        [PreserveSig] int GetConnectorCount(out uint n);
        [PreserveSig] int GetConnector(uint i, out IConnector conector);
    }

    [ComImport, Guid("9c2c4058-23f5-41de-877a-df3af236a09e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IConnector
    {
        [PreserveSig] int GetType(out int tipo);
        [PreserveSig] int GetDataFlow(out int fluxo);
        [PreserveSig] int ConnectTo(IConnector outro);
        [PreserveSig] int Disconnect();
        [PreserveSig] int IsConnected([MarshalAs(UnmanagedType.Bool)] out bool ligado);
        [PreserveSig] int GetConnectedTo(out IConnector outro);
        [PreserveSig] int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IKsControl
    {
        [PreserveSig] int KsProperty(ref KsPropriedade propriedade, uint tamanho, IntPtr dados, uint tamanhoDados, out uint devolvidos);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KsPropriedade { public Guid Conjunto; public uint Id; public uint Flags; }

    private static readonly Guid BtAudio = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

    private static IMMDeviceEnumerator Enumerador() =>
        (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;

    /// As saídas e entradas de áudio Bluetooth: nome, estado do endpoint e o filtro KS.
    public static List<(string Nome, uint Estado, string Filtro)> Listar()
    {
        var lista = new List<(string, uint, string)>();
        var e = Enumerador();
        e.EnumAudioEndpoints(2 /* eAll */, 0xF /* todos os estados */, out var col);
        col.GetCount(out uint n);
        for (uint i = 0; i < n; i++)
        {
            col.Item(i, out var d);
            d.GetId(out string id);
            d.GetState(out uint estado);
            string? filtro = Filtro(d);
            if (filtro is null || !filtro.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase)) continue;
            string nome;
            try { nome = Windows.Devices.Enumeration.DeviceInformation.CreateFromIdAsync(id).AsTask().Result.Name; }
            catch (AggregateException) { nome = filtro; } // saída ausente: o Windows não acha o nome
            lista.Add((nome, estado, filtro));
        }
        return lista;
    }

    private static string? Filtro(IMMDevice d)
    {
        var iid = typeof(IDeviceTopology).GUID;
        if (d.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var obj) != 0) return null;
        var topo = (IDeviceTopology)obj;
        if (topo.GetConnector(0, out var c) != 0) return null;
        return c.GetDeviceIdConnectedTo(out var filtro) == 0 ? filtro : null;
    }

    /// Pede ao Windows para conectar (true) ou desconectar o aparelho do filtro KS.
    public static int Pedir(string filtro, bool conectar)
    {
        var e = Enumerador();
        if (e.GetDevice(filtro, out var dispositivo) != 0) return -1;
        var iid = typeof(IKsControl).GUID;
        int hr = dispositivo.Activate(ref iid, 1, IntPtr.Zero, out var obj);
        if (hr != 0) return hr;
        var prop = new KsPropriedade { Conjunto = BtAudio, Id = conectar ? 0u : 1u, Flags = 1 /* KSPROPERTY_TYPE_GET */ };
        return ((IKsControl)obj).KsProperty(ref prop, (uint)Marshal.SizeOf<KsPropriedade>(), IntPtr.Zero, 0, out _);
    }
}

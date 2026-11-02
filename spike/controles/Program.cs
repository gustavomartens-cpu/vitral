// Prova dos controles do Win+A (DESCARTÁVEL). Uso: ProvaControles <radios|bt-lista|bt-alternar|aviao|dnd-ler|dnd-alternar|luz-ler|luz-alternar|ddc>
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

string cmd = args.FirstOrDefault() ?? "radios";
switch (cmd)
{
    case "radios":
        Console.WriteLine($"Acesso: {await Radio.RequestAccessAsync()}");
        foreach (var r in await Radio.GetRadiosAsync()) Console.WriteLine($"{r.Kind} \"{r.Name}\": {r.State}");
        break;

    case "bt-lista":
        foreach (var d in await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)))
        {
            var bt = await BluetoothDevice.FromIdAsync(d.Id);
            Console.WriteLine($"{d.Name} [{bt?.ClassOfDevice?.MajorClass}] {bt?.ConnectionStatus}");
        }
        foreach (var d in await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)))
        {
            var le = await BluetoothLEDevice.FromIdAsync(d.Id);
            Console.WriteLine($"{d.Name} [LE] {le?.ConnectionStatus}");
        }
        break;

    case "bt-alternar":
    {
        var bt = (await Radio.GetRadiosAsync()).FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        if (bt is null) { Console.WriteLine("sem rádio Bluetooth"); break; }
        var antes = bt.State;
        Console.WriteLine($"antes: {antes}; desligar: {await bt.SetStateAsync(antes == RadioState.On ? RadioState.Off : RadioState.On)}");
        await Task.Delay(2500);
        Console.WriteLine($"meio: {bt.State}; voltar: {await bt.SetStateAsync(antes)}");
        await Task.Delay(2000);
        Console.WriteLine($"depois: {bt.State}");
        break;
    }

    case "aviao":
    {
        // "Modo avião" pela API oficial: todos os rádios desligados, depois de volta como estavam.
        var radios = (await Radio.GetRadiosAsync()).ToList();
        var antes = radios.ToDictionary(r => r, r => r.State);
        foreach (var r in radios) Console.WriteLine($"desligar {r.Kind}: {await r.SetStateAsync(RadioState.Off)}");
        await Task.Delay(4000);
        foreach (var r in radios) Console.WriteLine($"meio {r.Kind}: {r.State}");
        foreach (var (r, s) in antes) Console.WriteLine($"voltar {r.Kind} para {s}: {await r.SetStateAsync(s)}");
        await Task.Delay(3000);
        foreach (var r in radios) Console.WriteLine($"depois {r.Kind}: {r.State}");
        break;
    }

    case "dnd-ler":
        Console.WriteLine($"Perfil: {QuietHours.Ler()}");
        break;

    case "dnd-alternar":
    {
        string antes = QuietHours.Ler();
        string outro = antes.EndsWith("Unrestricted") ? "Microsoft.QuietHoursProfile.PriorityOnly" : "Microsoft.QuietHoursProfile.Unrestricted";
        QuietHours.Gravar(outro);
        await Task.Delay(1500);
        Console.WriteLine($"antes: {antes}; meio: {QuietHours.Ler()}");
        QuietHours.Gravar(antes);
        await Task.Delay(1500);
        Console.WriteLine($"depois: {QuietHours.Ler()}");
        break;
    }

    case "luz-ler":
        Console.WriteLine($"Luz noturna ligada: {LuzNoturna.Ligada()}");
        break;

    case "luz-alternar":
    {
        bool antes = LuzNoturna.Ligada();
        LuzNoturna.Definir(!antes);
        await Task.Delay(3000);
        Console.WriteLine($"antes: {antes}; meio: {LuzNoturna.Ligada()}");
        LuzNoturna.Definir(antes);
        await Task.Delay(2000);
        Console.WriteLine($"depois: {LuzNoturna.Ligada()}");
        break;
    }

    case "ddc":
        Ddc.Listar();
        break;
}

/// "Não perturbe" (Assistente de foco) pelo COM não documentado do Windows, como fazem outros programas.
static class QuietHours
{
    [ComImport, Guid("6BFF4732-81EC-4FFB-AE67-B6C1BC29631F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IQuietHoursSettings
    {
        [PreserveSig] int get_UserSelectedProfile([MarshalAs(UnmanagedType.LPWStr)] out string perfil);
        [PreserveSig] int put_UserSelectedProfile([MarshalAs(UnmanagedType.LPWStr)] string perfil);
    }

    private static IQuietHoursSettings Criar() =>
        (IQuietHoursSettings)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("f53321fa-34f8-4b7f-b9a3-361877cb94cf"))!)!;

    public static string Ler()
    {
        int hr = Criar().get_UserSelectedProfile(out var p);
        return hr == 0 ? p : $"erro 0x{hr:X8}";
    }

    public static void Gravar(string perfil)
    {
        int hr = Criar().put_UserSelectedProfile(perfil);
        if (hr != 0) Console.WriteLine($"gravar: erro 0x{hr:X8}");
    }
}

/// Luz noturna: o estado fica num blob do CloudStore (formato conhecido da comunidade: byte 18 = 0x15 ligada, 0x13
/// desligada; ligada leva mais 2 bytes 0x10,0x00 na posição 23).
static class LuzNoturna
{
    private const string Chave = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate";

    public static bool Ligada() => Registry.CurrentUser.OpenSubKey(Chave)?.GetValue("Data") is byte[] d && d.Length > 18 && d[18] == 0x15;

    public static void Definir(bool ligar)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Chave, writable: true)!;
        var d = ((byte[])k.GetValue("Data")!).ToList();
        bool ligada = d[18] == 0x15;
        if (ligar == ligada) return;
        if (ligar) { d[18] = 0x15; d.InsertRange(23, [0x10, 0x00]); }
        else { d[18] = 0x13; d.RemoveRange(23, 2); }
        // Os bytes 10..14 são um carimbo de tempo: avança para o Windows aceitar a mudança.
        for (int i = 10; i < 15; i++) { if (d[i] != 0xFF) { d[i]++; break; } }
        k.SetValue("Data", d.ToArray(), RegistryValueKind.Binary);
    }
}

/// Brilho dos monitores externos por DDC/CI (dxva2): só lista o que cada monitor responde.
static class Ddc
{
    private delegate bool Visitar(IntPtr m, IntPtr dc, IntPtr r, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, Visitar v, IntPtr l);
    [DllImport("dxva2.dll")] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr m, out uint n);
    [DllImport("dxva2.dll")] private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr m, uint n, [Out] Fisico[] f);
    [DllImport("dxva2.dll")] private static extern bool GetMonitorBrightness(IntPtr f, out uint min, out uint atual, out uint max);
    [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitors(uint n, Fisico[] f);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Fisico { public IntPtr H; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Nome; }

    public static void Listar() => EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, _, _, _) =>
    {
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(m, out uint n) || n == 0) return true;
        var f = new Fisico[n];
        GetPhysicalMonitorsFromHMONITOR(m, n, f);
        foreach (var x in f)
            Console.WriteLine(GetMonitorBrightness(x.H, out var min, out var atual, out var max)
                ? $"{x.Nome}: brilho {atual} ({min}-{max})" : $"{x.Nome}: sem DDC/CI");
        DestroyPhysicalMonitors(n, f);
        return true;
    }, IntPtr.Zero);
}

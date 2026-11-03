using System.Runtime.InteropServices;
using Vitral.Core.Controles;
using Vitral.Ui.Barra;

namespace Vitral.App.Controles;

/// O brilho das telas (spec dos controles §3): a do notebook pelo WMI (pela interface COM do próprio Windows, sem pacote)
/// e os monitores externos com DDC/CI (dxva2). Tela sem controle não aparece.
internal static partial class Brilho
{
    private const string Notebook = "notebook";

    public static IReadOnlyList<TelaBrilho> Ler()
    {
        var telas = new List<TelaBrilho>();
        try
        {
            foreach (dynamic b in Wmi().ExecQuery("SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE"))
            {
                telas.Add(new TelaBrilho(Notebook, "Tela do notebook", (int)b.CurrentBrightness));
                break;
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
        int n = 0;
        PorMonitorFisico((saida, indice, nome, h) =>
        {
            n++;
            if (GetMonitorBrightness(h, out _, out uint atual, out uint max) && max > 0)
                telas.Add(new TelaBrilho(RegrasControles.IdTelaExterna(saida, indice),
                    string.IsNullOrWhiteSpace(nome) || nome == "Generic PnP Monitor" ? $"Monitor {n}" : nome,
                    (int)Math.Round(atual * 100.0 / max)));
        });
        return telas;
    }

    public static void Definir(string telaId, int valor)
    {
        try
        {
            if (telaId == Notebook)
            {
                foreach (dynamic m in Wmi().ExecQuery("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE"))
                    m.WmiSetBrightness(0, (byte)valor);
                return;
            }
            PorMonitorFisico((saida, indice, _, h) =>
            {
                if (RegrasControles.IdTelaExterna(saida, indice) != telaId || !GetMonitorBrightness(h, out uint min, out uint _, out uint max)) return;
                SetMonitorBrightness(h, (uint)(min + (max - min) * valor / 100.0));
            });
        }
        catch (Exception e) when (e is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            Barra.LogBarra.Erro("Brilho.Definir", e);
        }
    }

    private static dynamic Wmi()
    {
        dynamic localizador = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator")!)!;
        return localizador.ConnectServer(".", @"root\wmi");
    }

    private delegate bool Visitar(IntPtr monitor, IntPtr dc, IntPtr ret, IntPtr l);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool EnumDisplayMonitors(IntPtr dc, IntPtr corte, Visitar v, IntPtr l);
    [LibraryImport("dxva2.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint n);
    [DllImport("dxva2.dll")] private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint n, [Out] Fisico[] fisicos);
    [LibraryImport("dxva2.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetMonitorBrightness(IntPtr h, out uint min, out uint atual, out uint max);
    [LibraryImport("dxva2.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetMonitorBrightness(IntPtr h, uint valor);
    [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitors(uint n, Fisico[] fisicos);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Fisico { public IntPtr H; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Nome; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InfoMonitor
    {
        public int Tamanho;
        public int L1, T1, R1, B1, L2, T2, R2, B2;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Saida;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfoW(IntPtr monitor, ref InfoMonitor info);

    /// Cada monitor físico com a saída de vídeo do Windows ("\\.\DISPLAY2") e a posição dentro dela: o id da tela não
    /// muda quando outro monitor é desligado.
    private static void PorMonitorFisico(Action<string, int, string, IntPtr> fazer) =>
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, _, _, _) =>
        {
            var info = new InfoMonitor { Tamanho = Marshal.SizeOf<InfoMonitor>() };
            if (!GetMonitorInfoW(m, ref info)) return true;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(m, out uint n) || n == 0) return true;
            var f = new Fisico[n];
            if (!GetPhysicalMonitorsFromHMONITOR(m, n, f)) return true;
            try { for (int i = 0; i < f.Length; i++) fazer(info.Saida, i, f[i].Nome, f[i].H); }
            finally { DestroyPhysicalMonitors(n, f); }
            return true;
        }, IntPtr.Zero);
}

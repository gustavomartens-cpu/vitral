using System.Runtime.InteropServices;
using System.Windows.Threading;
using Vitral.Ui.Barra;
using Windows.Networking.Connectivity;
using Windows.System.Power;

namespace Vitral.App.Barra;

/// Rede pela conexão com a internet (Wi-Fi, cabo ou nenhuma).
internal sealed class FonteRedeWindows : IFonteRede, IDisposable
{
    private readonly Dispatcher _ui;

    public TipoRede? Atual { get; private set; }
    public event Action? Mudou;

    public FonteRedeWindows(Dispatcher ui)
    {
        _ui = ui;
        Ler();
        try { NetworkInformation.NetworkStatusChanged += AoMudar; }
        catch (Exception e) { LogBarra.Erro("Rede.Iniciar", e); }
    }

    private void AoMudar(object? remetente) => _ui.BeginInvoke(() => { Ler(); Mudou?.Invoke(); });

    private void Ler()
    {
        try
        {
            var perfil = NetworkInformation.GetInternetConnectionProfile();
            Atual = perfil is null ? TipoRede.Desconectada : perfil.IsWlanConnectionProfile ? TipoRede.Wifi : TipoRede.Cabo;
        }
        catch (Exception e)
        {
            LogBarra.Erro("Rede.Ler", e);
            Atual = null;
        }
    }

    public void Dispose()
    {
        try { NetworkInformation.NetworkStatusChanged -= AoMudar; }
        catch (Exception e) { LogBarra.Erro("Rede.Dispose", e); }
    }
}

/// Bateria pelo PowerManager; num desktop sem bateria, Atual é null e a bateria não aparece.
internal sealed class FonteBateriaWindows : IFonteBateria, IDisposable
{
    private readonly Dispatcher _ui;

    public EstadoBateria? Atual { get; private set; }
    public event Action? Mudou;

    public FonteBateriaWindows(Dispatcher ui)
    {
        _ui = ui;
        Ler();
        try
        {
            PowerManager.RemainingChargePercentChanged += AoMudar;
            PowerManager.BatteryStatusChanged += AoMudar;
            PowerManager.PowerSupplyStatusChanged += AoMudar;
        }
        catch (Exception e) { LogBarra.Erro("Bateria.Iniciar", e); }
    }

    private void AoMudar(object? remetente, object? args) => _ui.BeginInvoke(() => { Ler(); Mudou?.Invoke(); });

    private void Ler()
    {
        try
        {
            Atual = PowerManager.BatteryStatus == BatteryStatus.NotPresent
                ? null
                : new EstadoBateria(PowerManager.RemainingChargePercent, PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent,
                    TempoRestante());
        }
        catch (Exception e)
        {
            LogBarra.Erro("Bateria.Ler", e);
            Atual = null;
        }
    }

    /// Null carregando ou quando o Windows ainda não estimou (zero ou "infinito").
    private static TimeSpan? TempoRestante()
    {
        if (PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent) return null;
        var t = PowerManager.RemainingDischargeTime;
        return t <= TimeSpan.Zero || t == TimeSpan.MaxValue || t.TotalDays > 7 ? null : t;
    }

    public void Dispose()
    {
        try
        {
            PowerManager.RemainingChargePercentChanged -= AoMudar;
            PowerManager.BatteryStatusChanged -= AoMudar;
            PowerManager.PowerSupplyStatusChanged -= AoMudar;
        }
        catch (Exception e) { LogBarra.Erro("Bateria.Dispose", e); }
    }
}

/// CPU por GetSystemTimes (diferença entre duas leituras; a primeira devolve null) e memória por GlobalMemoryStatusEx.
internal sealed partial class FonteDesempenhoWindows : IFonteDesempenho
{
    [StructLayout(LayoutKind.Sequential)]
    private struct EstadoMemoria
    {
        public uint Tamanho, Carga;
        public ulong FisicaTotal, FisicaLivre, PaginacaoTotal, PaginacaoLivre, VirtualTotal, VirtualLivre, Estendida;
    }

    [LibraryImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out long ocioso, out long nucleo, out long usuario);
    [LibraryImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref EstadoMemoria estado);

    private (long Ocioso, long Total)? _anterior;

    public Desempenho? Ler()
    {
        try
        {
            if (!GetSystemTimes(out long ocioso, out long nucleo, out long usuario)) return null;
            long total = nucleo + usuario; // o tempo de núcleo já inclui o ocioso
            var anterior = _anterior;
            _anterior = (ocioso, total);
            if (anterior is not { } a || total - a.Total <= 0) return null;
            double cpu = 1 - (double)(ocioso - a.Ocioso) / (total - a.Total);

            var memoria = new EstadoMemoria { Tamanho = (uint)Marshal.SizeOf<EstadoMemoria>() };
            if (!GlobalMemoryStatusEx(ref memoria)) return null;
            return new Desempenho(Math.Clamp(cpu, 0, 1), memoria.Carga / 100.0);
        }
        catch (Exception e)
        {
            LogBarra.Erro("Desempenho.Ler", e);
            return null;
        }
    }
}

internal sealed class RelogioDoSistema : IRelogioDoSistema
{
    public DateTime Agora => DateTime.Now;
}

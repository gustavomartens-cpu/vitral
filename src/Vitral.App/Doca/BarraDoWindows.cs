using System.Runtime.InteropServices;
using System.Windows.Threading;
using Vitral.App.Barra;
using Vitral.Core.Barra;
using Vitral.Core.Nativo;
using Vitral.Core.Registro;

namespace Vitral.App.Doca;

/// A barra de tarefas do Windows vista de fora (spec da doca §4): esconde as do Explorer (a principal e as dos outros
/// monitores, filtradas pelo processo, porque a bandeja do Vitral também tem uma "Shell_TrayWnd"), vigia para
/// esconder de novo e liga/desliga o "ocultar automaticamente". Nada roda dentro do Explorer.
internal sealed partial class BarraDoWindows : IBarraDoWindows, IDisposable
{
    private const int SwHide = 0, SwShowNa = 8;
    private const uint AbmGetState = 4;
    private const int AbsAutoHide = 1;

    [StructLayout(LayoutKind.Sequential)] private struct Ret { public int Esq, Topo, Dir, Base; }
    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData { public uint Tamanho; public IntPtr Janela; public uint MensagemRetorno; public uint Borda; public Ret Retangulo; public IntPtr Parametro; }

    [LibraryImport("shell32.dll")] private static partial UIntPtr SHAppBarMessage(uint mensagem, ref AppBarData dados);
    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr FindWindowEx(IntPtr pai, IntPtr depoisDe, string classe, string? titulo);
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(IntPtr janela, out uint pid);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool ShowWindow(IntPtr janela, int comando);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWindowVisible(IntPtr janela);

    private readonly IRegistro _registro;
    private readonly INotificador _notificador;
    private readonly DispatcherTimer _vigia = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _escondida;
    private bool _registrouFalha;

    public BarraDoWindows(IRegistro registro, INotificador notificador)
    {
        _registro = registro;
        _notificador = notificador;
        _vigia.Tick += (_, _) => Vigiar();
    }

    /// As barras do Explorer agora (a principal primeiro).
    private static List<IntPtr> Barras()
    {
        uint eu = (uint)Environment.ProcessId;
        var lista = new List<IntPtr>();
        foreach (var classe in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        {
            IntPtr h = IntPtr.Zero;
            while ((h = FindWindowEx(IntPtr.Zero, h, classe, null)) != IntPtr.Zero)
                if (GetWindowThreadProcessId(h, out uint pid) != 0 && pid != eu) lista.Add(h);
        }
        return lista;
    }

    public bool? OcultarAutomatico()
    {
        try
        {
            var principal = Barras().FirstOrDefault();
            if (principal == IntPtr.Zero) return null;
            var d = new AppBarData { Tamanho = (uint)Marshal.SizeOf<AppBarData>(), Janela = principal };
            return ((ulong)SHAppBarMessage(AbmGetState, ref d) & AbsAutoHide) != 0;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("BarraDoWindows.Ler", e); return null; }
    }

    /// Grava o StuckRects3 (o Explorer não regrava ao desligar — teste de 25/10) e avisa a barra na hora.
    public void DefinirOcultarAutomatico(bool ligado)
    {
        try
        {
            var atual = _registro.Ler(Core.Nativo.BarraDeTarefas.StuckRects, "Settings", TipoValor.Binario).Dado as byte[];
            var valores = Core.Nativo.BarraDeTarefas.Calcular(new Core.Perfis.BarraTarefasPerfil(OcultarAutomaticamente: ligado), atual);
            foreach (var v in valores) _registro.Gravar(v);
            _notificador.AvisarOcultarAutomatico(ligado);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("BarraDoWindows.Ocultar", e); }
    }

    public void Esconder()
    {
        _escondida = true;
        Aplicar(SwHide);
        _vigia.Start();
    }

    public void Mostrar()
    {
        _escondida = false;
        _vigia.Stop();
        Aplicar(SwShowNa);
    }

    /// Explorer reiniciou ou monitores mudaram: esconde de novo, se for o caso.
    public void ReaplicarSeEscondida()
    {
        if (_escondida) Aplicar(SwHide);
    }

    private void Vigiar()
    {
        try
        {
            foreach (var h in Barras())
                if (IsWindowVisible(h)) ShowWindow(h, SwHide);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("BarraDoWindows.Vigiar", e); }
    }

    private void Aplicar(int comando)
    {
        try
        {
            var barras = Barras();
            if (barras.Count == 0)
            {
                if (!_registrouFalha) LogBarra.Info("Nenhuma barra do Explorer encontrada; tento de novo na vigilância.");
                _registrouFalha = true;
                return;
            }
            _registrouFalha = false;
            foreach (var h in barras) ShowWindow(h, comando);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("BarraDoWindows.Aplicar", e); }
    }

    public void Dispose() => _vigia.Stop();
}

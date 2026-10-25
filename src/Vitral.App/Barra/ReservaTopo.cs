using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Vitral.App.Barra;

/// Reserva a faixa do topo do monitor principal como AppBar (spec §3.1): janelas maximizadas começam abaixo dela.
/// Uma janela oculta recebe as notificações da AppBar (tela cheia, mudança de posição) e o "TaskbarCreated"
/// do Explorer reiniciado. Se o Vitral cair, o Windows libera a reserva sozinho.
internal sealed partial class ReservaTopo : IDisposable
{
    private const uint AbmNew = 0, AbmRemove = 1, AbmQueryPos = 2, AbmSetPos = 3;
    private const uint AbeTop = 1;
    private const int AbnPosChanged = 1, AbnFullScreenApp = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Ret { public int Esq, Topo, Dir, Base; }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Tamanho;
        public IntPtr Janela;
        public uint MensagemRetorno;
        public uint Borda;
        public Ret Retangulo;
        public IntPtr Parametro;
    }

    [LibraryImport("shell32.dll")] private static partial UIntPtr SHAppBarMessage(uint mensagem, ref AppBarData dados);
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string nome);

    private readonly HwndSource _janela;
    private readonly uint _mensagemAppBar = RegisterWindowMessage("Vitral.ReservaTopo");
    private readonly uint _mensagemTaskbarCriada = RegisterWindowMessage("TaskbarCreated");
    private bool _registrada;

    /// true quando um app entra em tela cheia; false quando sai.
    public event Action<bool>? TelaCheia;

    /// O Explorer reiniciou ou a posição mudou: registrar de novo e reposicionar as ilhas.
    public event Action? Recriar;

    public ReservaTopo()
    {
        // Janela comum oculta (não "message-only"): precisa receber as mensagens de difusão, como TaskbarCreated.
        _janela = new HwndSource(new HwndSourceParameters("Vitral.ReservaTopo") { Width = 0, Height = 0, WindowStyle = 0 });
        _janela.AddHook(Processar);
    }

    public bool Registrada => _registrada;

    /// Registra (se preciso) e posiciona a faixa de 46 px. Devolve false se o Windows recusar.
    public bool Registrar()
    {
        var d = Dados();
        if (!_registrada)
        {
            d.MensagemRetorno = _mensagemAppBar;
            if (SHAppBarMessage(AbmNew, ref d) == UIntPtr.Zero) return false;
            _registrada = true;
        }
        Reposicionar();
        return true;
    }

    public void Reposicionar()
    {
        if (!_registrada) return;
        var monitor = MonitorPrincipal.Ler(); // escala e tamanho atuais do principal, não os da janela oculta
        int altura = (int)Math.Round(Ui.Barra.PosicaoIlhas.Reserva * monitor.Dpi / 96.0);
        var d = Dados();
        d.Borda = AbeTop;
        d.Retangulo = new Ret { Esq = monitor.X, Topo = monitor.Y, Dir = monitor.X + monitor.Largura, Base = monitor.Y + altura };
        SHAppBarMessage(AbmQueryPos, ref d);
        d.Retangulo.Base = d.Retangulo.Topo + altura; // o Windows pode ter empurrado o topo; a altura é nossa
        SHAppBarMessage(AbmSetPos, ref d);
    }

    public void Liberar()
    {
        if (!_registrada) return;
        var d = Dados();
        SHAppBarMessage(AbmRemove, ref d);
        _registrada = false;
    }

    /// O aviso também chega com a área de trabalho em primeiro plano ou tela cheia em outro monitor: confere.
    private static bool TelaCheiaDeVerdade()
    {
        var janela = GetForegroundWindow();
        if (janela == IntPtr.Zero || !GetWindowRect(janela, out var r)) return false;
        string classe = NomeDaClasse(janela);
        var m = MonitorPrincipal.Ler();
        bool real = Ui.Barra.TelaCheia.EhDeVerdade(classe,
            new Ui.Barra.Retangulo(r.Esq, r.Topo, r.Dir - r.Esq, r.Base - r.Topo), new Ui.Barra.Retangulo(m.X, m.Y, m.Largura, m.Altura));
        if (!real) LogBarra.Info($"Aviso de tela cheia ignorado ({classe})");
        return real;
    }

    private static unsafe string NomeDaClasse(IntPtr janela)
    {
        char* buffer = stackalloc char[256];
        int n = GetClassName(janela, buffer, 256);
        return n > 0 ? new string(buffer, 0, n) : "";
    }

    [LibraryImport("user32.dll")] private static partial IntPtr GetForegroundWindow();
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetWindowRect(IntPtr janela, out Ret ret);
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")] private static unsafe partial int GetClassName(IntPtr janela, char* classe, int tamanho);

    private AppBarData Dados() => new() { Tamanho = (uint)Marshal.SizeOf<AppBarData>(), Janela = _janela.Handle };

    private IntPtr Processar(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool tratado)
    {
        try
        {
            if (msg == _mensagemAppBar)
            {
                if ((int)wParam == AbnFullScreenApp) TelaCheia?.Invoke(lParam != IntPtr.Zero && TelaCheiaDeVerdade());
                else if ((int)wParam == AbnPosChanged) Reposicionar();
            }
            else if (msg == _mensagemTaskbarCriada)
            {
                _registrada = false; // o Explorer novo não conhece a reserva antiga
                Recriar?.Invoke();
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("ReservaTopo", e);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Liberar();
        _janela.RemoveHook(Processar);
        _janela.Dispose();
    }
}

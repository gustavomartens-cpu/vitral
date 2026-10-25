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
    private const int SmCxScreen = 0;

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
    [LibraryImport("user32.dll")] private static partial int GetSystemMetrics(int indice);
    [LibraryImport("user32.dll")] private static partial uint GetDpiForWindow(IntPtr janela);

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
        double escala = GetDpiForWindow(_janela.Handle) / 96.0;
        if (escala <= 0) escala = 1;
        int altura = (int)Math.Round(Ui.Barra.PosicaoIlhas.Reserva * escala);
        var d = Dados();
        d.Borda = AbeTop;
        d.Retangulo = new Ret { Esq = 0, Topo = 0, Dir = GetSystemMetrics(SmCxScreen), Base = altura };
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

    private AppBarData Dados() => new() { Tamanho = (uint)Marshal.SizeOf<AppBarData>(), Janela = _janela.Handle };

    private IntPtr Processar(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool tratado)
    {
        try
        {
            if (msg == _mensagemAppBar)
            {
                if ((int)wParam == AbnFullScreenApp) TelaCheia?.Invoke(lParam != IntPtr.Zero);
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

using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Vitral.App.Barra;

/// Reserva uma faixa do monitor principal como AppBar: a do topo (barra do topo, spec §3.1) ou a de baixo (doca):
/// janelas maximizadas ficam fora dela. Uma janela oculta recebe as notificações da AppBar (tela cheia, mudança de posição) e o "TaskbarCreated"
/// do Explorer reiniciado. Se o Vitral cair, o Windows libera a reserva sozinho.
internal sealed partial class ReservaFaixa : Ui.Barra.IAppBar, IDisposable
{
    private const uint AbmNew = 0, AbmRemove = 1, AbmQueryPos = 2, AbmSetPos = 3;
    private const uint AbeTop = 1, AbeBottom = 3;
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
    private readonly uint _mensagemAppBar = RegisterWindowMessage("Vitral.ReservaFaixa");
    private readonly uint _mensagemTaskbarCriada = RegisterWindowMessage("TaskbarCreated");
    private readonly Ui.Barra.RegistroAppBar _registro;

    /// true quando um app entra em tela cheia; false quando sai.
    public event Action<bool>? TelaCheia;

    /// O Explorer reiniciou ou a posição mudou: registrar de novo e reposicionar as ilhas.
    public event Action? Recriar;

    private readonly bool _embaixo;
    private readonly Func<double> _alturaDips;

    /// <param name="embaixo">Faixa de baixo (doca) em vez da de cima.</param>
    /// <param name="alturaDips">Lida a cada registro e reposicionamento: acompanha as formas (spec das formas §4.4).</param>
    public ReservaFaixa(bool embaixo, Func<double> alturaDips)
    {
        _embaixo = embaixo;
        _alturaDips = alturaDips;
        _registro = new Ui.Barra.RegistroAppBar(this);
        // Janela comum oculta (não "message-only"): precisa receber as mensagens de difusão, como TaskbarCreated.
        _janela = new HwndSource(new HwndSourceParameters("Vitral.ReservaFaixa") { Width = 0, Height = 0, WindowStyle = 0 });
        _janela.AddHook(Processar);
        _conferencia.Tick += (_, _) => ConferirTelaCheia(registrar: false);
        _conferencia.Start();
    }

    // Tela cheia: o Vitral confere sozinho se a janela da frente cobre o monitor principal; o aviso do Windows só adianta
    // a conferência. Ele falha com alguns jogos e manda "saiu" quando um overlay abre (31/10).
    private readonly System.Windows.Threading.DispatcherTimer _conferencia = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly Ui.Barra.VigiaTelaCheia _vigia = new();

    private void ConferirTelaCheia(bool registrar)
    {
        if (_vigia.Conferir(TelaCheiaDeVerdade(registrar)) is { } mudou) TelaCheia?.Invoke(mudou);
    }

    public bool Registrada => _registro.Registrada;

    /// Registra (se preciso) e posiciona a faixa. Devolve false se o Windows recusar.
    public bool Registrar() => _registro.Registrar();

    public void Reposicionar() => _registro.Reposicionar();

    public void Liberar() => _registro.Liberar();

    bool Ui.Barra.IAppBar.Novo()
    {
        var d = Dados();
        d.MensagemRetorno = _mensagemAppBar;
        _ultimoSetPos = default; // registro novo: a primeira faixa sempre vai
        return SHAppBarMessage(AbmNew, ref d) != UIntPtr.Zero;
    }

    void Ui.Barra.IAppBar.Remover()
    {
        var d = Dados();
        SHAppBarMessage(AbmRemove, ref d);
    }

    void Ui.Barra.IAppBar.Posicionar()
    {
        var monitor = MonitorPrincipal.Ler(); // escala e tamanho atuais do principal, não os da janela oculta
        int altura = (int)Math.Round(_alturaDips() * monitor.Dpi / 96.0);
        int fundo = monitor.Y + monitor.Altura;
        var d = Dados();
        d.Borda = _embaixo ? AbeBottom : AbeTop;
        d.Retangulo = _embaixo
            ? new Ret { Esq = monitor.X, Topo = fundo - altura, Dir = monitor.X + monitor.Largura, Base = fundo }
            : new Ret { Esq = monitor.X, Topo = monitor.Y, Dir = monitor.X + monitor.Largura, Base = monitor.Y + altura };
        SHAppBarMessage(AbmQueryPos, ref d);
        // O Windows pode ter empurrado a borda; a altura é nossa.
        if (_embaixo) d.Retangulo.Topo = d.Retangulo.Base - altura;
        else d.Retangulo.Base = d.Retangulo.Topo + altura;
        // A mesma faixa de novo logo em seguida não muda nada e só realimenta os avisos de posição (ver AgendarReposicionar).
        var agora = DateTime.UtcNow;
        if (d.Retangulo.Equals(_ultimaFaixa) && agora - _ultimoSetPos < TimeSpan.FromSeconds(1)) return;
        (_ultimaFaixa, _ultimoSetPos) = (d.Retangulo, agora);
        SHAppBarMessage(AbmSetPos, ref d);
    }

    private Ret _ultimaFaixa;
    private DateTime _ultimoSetPos;

    /// O aviso também chega com a área de trabalho em primeiro plano ou tela cheia em outro monitor: confere. Pela ordem
    /// das janelas no monitor principal, e não pela janela da frente: clicar no outro monitor não tira a tela cheia (02/11).
    private static bool TelaCheiaDeVerdade(bool registrar)
    {
        var m = MonitorPrincipal.Ler();
        var janelas = new List<(string Classe, Ui.Barra.Retangulo Janela)>();
        uint proprio = (uint)Environment.ProcessId;
        EnumWindows((h, _) =>
        {
            if (Conta(h, proprio) && GetWindowRect(h, out var r))
                janelas.Add((NomeDaClasse(h), new Ui.Barra.Retangulo(r.Esq, r.Topo, r.Dir - r.Esq, r.Base - r.Topo)));
            return true;
        }, IntPtr.Zero);
        bool real = Ui.Barra.TelaCheia.NoPrincipal(janelas, new Ui.Barra.Retangulo(m.X, m.Y, m.Largura, m.Altura));
        if (!real && registrar)
            LogBarra.Info($"Aviso de tela cheia do Windows sem janela cobrindo o monitor ({janelas.FirstOrDefault().Classe})");
        return real;
    }

    private const int GwlExStyle = -20, DwmCloaked = 14;
    private const long Ferramenta = 0x80, Transparente = 0x20;

    /// Uma janela que alguém vê: visível, não minimizada nem escondida pelo Windows, fora das sobreposições (ferramenta ou
    /// transparente ao mouse) e das do próprio Vitral (notch, doca, ilhas).
    private static bool Conta(IntPtr h, uint proprio)
    {
        if (!IsWindowVisible(h) || IsIconic(h)) return false;
        GetWindowThreadProcessId(h, out uint processo);
        if (processo == proprio) return false;
        if (DwmGetWindowAttribute(h, DwmCloaked, out int escondida, sizeof(int)) == 0 && escondida != 0) return false;
        return (GetWindowLongPtr(h, GwlExStyle) & (Ferramenta | Transparente)) == 0;
    }

    private delegate bool Visitar(IntPtr h, IntPtr l);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool EnumWindows(Visitar v, IntPtr l);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWindowVisible(IntPtr h);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsIconic(IntPtr h);
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(IntPtr h, out uint processo);
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial long GetWindowLongPtr(IntPtr h, int i);
    [LibraryImport("dwmapi.dll")] private static partial int DwmGetWindowAttribute(IntPtr h, int atributo, out int valor, int tamanho);

    private static unsafe string NomeDaClasse(IntPtr janela)
    {
        char* buffer = stackalloc char[256];
        int n = GetClassName(janela, buffer, 256);
        return n > 0 ? new string(buffer, 0, n) : "";
    }

    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetWindowRect(IntPtr janela, out Ret ret);
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")] private static unsafe partial int GetClassName(IntPtr janela, char* classe, int tamanho);

    private bool _reposicionarPendente;
    private int _avisosNoSegundo;
    private DateTime _inicioDoSegundo;

    /// Junta os avisos de posição num só reposicionamento, quando a fila esvazia. Responder a cada um com ABM_SETPOS
    /// pode virar vaivém com o Explorer (o SETPOS gera novos avisos) e encher a fila de mensagens da thread da
    /// interface: depois da suspensão o app travou e caiu com "Não há cota suficiente" (29/10). Rajadas vão para o log.
    private void AgendarReposicionar()
    {
        var agora = DateTime.UtcNow;
        if (agora - _inicioDoSegundo > TimeSpan.FromSeconds(1))
        {
            if (_avisosNoSegundo > 20) LogBarra.Info($"Rajada de avisos de posição da AppBar: {_avisosNoSegundo} em 1 s");
            (_inicioDoSegundo, _avisosNoSegundo) = (agora, 0);
        }
        _avisosNoSegundo++;
        if (_reposicionarPendente) return;
        _reposicionarPendente = true;
        _janela.Dispatcher.BeginInvoke(() =>
        {
            _reposicionarPendente = false;
            try { Reposicionar(); }
            catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ReservaFaixa", e); }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private AppBarData Dados() => new() { Tamanho = (uint)Marshal.SizeOf<AppBarData>(), Janela = _janela.Handle };

    private IntPtr Processar(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool tratado)
    {
        try
        {
            if (msg == _mensagemAppBar)
            {
                if ((int)wParam == AbnFullScreenApp) ConferirTelaCheia(registrar: lParam != IntPtr.Zero);
                else if ((int)wParam == AbnPosChanged) AgendarReposicionar();
            }
            else if (msg == _mensagemTaskbarCriada)
            {
                _registro.AoCriarBarra(); // do Explorer reiniciado ou da bandeja do Vitral (ver RegistroAppBar)
                Recriar?.Invoke();
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("ReservaFaixa", e);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _conferencia.Stop();
        Liberar();
        _janela.RemoveHook(Processar);
        _janela.Dispose();
    }
}

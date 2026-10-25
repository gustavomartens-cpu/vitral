using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;
using ManagedShell;
using ManagedShell.WindowsTasks;
using ManagedShell.WindowsTray;
using Vitral.App.Barra;
using Vitral.Ui.Barra;

namespace Vitral.App.Doca;

/// Bandeja dos apps (spec da doca §7): com a doca ligada, o Vitral é o dono da bandeja pela ManagedShell
/// (TrayService: a própria "Shell_TrayWnd" + TaskbarCreated; nada entra no Explorer). Ao desligar, devolve os ícones
/// ao Windows com um novo TaskbarCreated. O mesmo ShellManager traz a lista de janelas da doca (TasksService).
internal sealed partial class FonteBandeja(Dispatcher ui) : IFonteBandeja, IDisposable
{
    /// Ícones do Windows que a ilha de status do topo já mostra: ficam de fora do painel.
    private static readonly HashSet<Guid> JaNoTopo =
        [new(NotificationArea.VOLUME_GUID), new(NotificationArea.POWER_GUID), new(NotificationArea.NETWORK_GUID)];

    [LibraryImport("user32.dll")] private static partial uint GetDoubleClickTime();
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetCursorPos(out Ponto ponto);
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string nome);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SendNotifyMessage(IntPtr janela, uint mensagem, IntPtr w, IntPtr l);

    [StructLayout(LayoutKind.Sequential)] private struct Ponto { public int X, Y; }

    private ShellManager? _shell;

    // Lista de janelas da doca. O TasksService registra uma DependencyProperty no construtor de instância, então só
    // pode existir um por processo: é criado uma vez e religado (Initialize) a cada vez que a doca liga.
    private TasksService? _servicoTarefas;
    private Tasks? _tarefas;
    private bool _tarefasLigadas;

    public ShellManager? Shell => _shell;

    /// As janelas para a doca; null com a doca desligada ou se o serviço não subiu.
    public Tasks? Tarefas => _tarefasLigadas ? _tarefas : null;

    public TasksService? ServicoTarefas => _tarefasLigadas ? _servicoTarefas : null;
    public bool Disponivel => _shell is { NotificationArea.IsFailed: false };
    public event Action? Mudou;

    public IReadOnlyList<IconeBandeja> Icones =>
        _shell?.NotificationArea.TrayIcons.ToList()
            .Select(i => new IconeBandeja(i.Identifier, i.Title ?? "", i.Icon, JaNoTopo.Contains(i.GUID)))
            .ToList() ?? [];

    public void Ligar()
    {
        if (_shell is not null) return;
        try
        {
            var config = ShellManager.DefaultShellConfig;
            config.EnableTasksService = false; // o nosso, abaixo
            _shell = new ShellManager(config);
            _shell.NotificationArea.TrayIcons.CollectionChanged += AoMudar;
            if (_shell.NotificationArea.IsFailed) LogBarra.Info("A bandeja não iniciou (ManagedShell IsFailed).");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("Bandeja.Ligar", e);
            _shell = null;
        }
        try
        {
            _servicoTarefas ??= new TasksService();
            _tarefas ??= new Tasks(_servicoTarefas);
            _tarefas.Initialize(false);
            _tarefasLigadas = true;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("Bandeja.Tarefas", e); }
        Mudou?.Invoke();
    }

    public void Desligar()
    {
        if (_tarefasLigadas)
        {
            _tarefasLigadas = false;
            Mudou?.Invoke(); // a doca larga a lista antes de ela ser esvaziada
            try { _servicoTarefas?.Dispose(); } // desfaz os ganchos; o mesmo serviço religa na próxima vez
            catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("Bandeja.Tarefas", e); }
        }
        if (_shell is null) return;
        try
        {
            _shell.NotificationArea.TrayIcons.CollectionChanged -= AoMudar;
            _shell.Dispose();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("Bandeja.Desligar", e); }
        _shell = null;
        DevolverIcones();
        Mudou?.Invoke();
    }

    /// TaskbarCreated para todos: os apps registram os ícones de novo, agora na barra do Windows.
    public static void DevolverIcones() =>
        SendNotifyMessage(new IntPtr(0xFFFF), RegisterWindowMessage("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);

    private void AoMudar(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        ui.BeginInvoke(() => Mudou?.Invoke());

    public void Clicar(string id, BotaoMouse botao)
    {
        try
        {
            var icone = _shell?.NotificationArea.TrayIcons.FirstOrDefault(i => i.Identifier == id);
            if (icone is null) return;
            var b = botao == BotaoMouse.Direito ? MouseButton.Right : MouseButton.Left;
            GetCursorPos(out var p);
            uint posicao = (uint)((p.Y << 16) | (p.X & 0xFFFF));
            int duplo = (int)GetDoubleClickTime();
            icone.IconMouseDown(b, posicao, duplo);
            icone.IconMouseUp(b, posicao, duplo);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("Bandeja.Clicar", e); }
    }

    public void Dispose() => Desligar();
}

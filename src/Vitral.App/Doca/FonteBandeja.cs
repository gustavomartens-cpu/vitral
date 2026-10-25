using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;
using ManagedShell;
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

    public ShellManager? Shell => _shell;
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
            config.EnableTasksService = true;
            config.AutoStartTasksService = true;
            _shell = new ShellManager(config);
            _shell.NotificationArea.TrayIcons.CollectionChanged += AoMudar;
            if (_shell.NotificationArea.IsFailed) LogBarra.Info("A bandeja não iniciou (ManagedShell IsFailed).");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("Bandeja.Ligar", e);
            _shell = null;
        }
        Mudou?.Invoke();
    }

    public void Desligar()
    {
        if (_shell is null) return;
        try
        {
            _shell.NotificationArea.TrayIcons.CollectionChanged -= AoMudar;
            _shell.Dispose();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("Bandeja.Desligar", e); }
        _shell = null;
        // Os apps registram os ícones de novo, agora na barra do Windows.
        SendNotifyMessage(new IntPtr(0xFFFF), RegisterWindowMessage("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        Mudou?.Invoke();
    }

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

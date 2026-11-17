using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Threading;
using ManagedShell.WindowsTasks;
using Vitral.App.Barra;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

/// Janelas da doca (spec da doca §5): a lista filtrada da ManagedShell (Tasks.GroupedWindows, as mesmas regras da
/// barra do Windows: ilhas, doca e painéis do Vitral ficam de fora por serem janelas de ferramenta). Avisa uma vez por rodada de mudanças, na thread da interface.
internal sealed partial class FonteJanelas : IFonteJanelas, IDisposable
{
    private readonly FonteBandeja _bandeja;
    private readonly Dispatcher _ui;
    private Tasks? _tasks;
    private List<ApplicationWindow> _vigiadas = [];
    private bool _avisoPendente;

    public event Action? Mudou;

    public FonteJanelas(FonteBandeja bandeja, Dispatcher ui)
    {
        _bandeja = bandeja;
        _ui = ui;
        _bandeja.Mudou += Anexar;
        Anexar();
    }

    public IReadOnlyList<JanelaApp> Janelas => _vigiadas.Select(Mapear).ToList();

    public IntPtr Ativa => _vigiadas.FirstOrDefault(j => j.State == ApplicationWindow.WindowState.Active)?.Handle ?? IntPtr.Zero;

    private static JanelaApp Mapear(ApplicationWindow j)
    {
        var exe = j.WinFileName ?? "";
        var appId = string.IsNullOrEmpty(j.AppUserModelID) ? null : j.AppUserModelID;
        // Sem AppId nem executável não há como agrupar: a janela fica sozinha.
        if (appId is null && exe.Length == 0) appId = $"janela:{j.Handle}";
        return new JanelaApp(j.Handle, j.Title ?? "", appId, exe, j.IsMinimized,
            j.State == ApplicationWindow.WindowState.Flashing, j.Icon);
    }

    /// Liga na lista da ManagedShell atual (a bandeja cria e desfaz o ShellManager).
    private void Anexar()
    {
        var tasks = _bandeja.Tarefas;
        if (ReferenceEquals(tasks, _tasks)) return;
        Desanexar();
        _tasks = tasks;
        if (_tasks?.GroupedWindows is INotifyCollectionChanged lista) lista.CollectionChanged += AoMudarLista;
        Recarregar();
    }

    private void Desanexar()
    {
        if (_tasks?.GroupedWindows is INotifyCollectionChanged lista) lista.CollectionChanged -= AoMudarLista;
        foreach (var j in _vigiadas) j.PropertyChanged -= AoMudarJanela;
        _vigiadas = [];
        _tasks = null;
    }

    private void AoMudarLista(object? s, NotifyCollectionChangedEventArgs e) => Recarregar();

    private void Recarregar()
    {
        try
        {
            foreach (var j in _vigiadas) j.PropertyChanged -= AoMudarJanela;
            _vigiadas = _tasks?.GroupedWindows?.Cast<ApplicationWindow>().ToList() ?? [];
            foreach (var j in _vigiadas) j.PropertyChanged += AoMudarJanela;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("FonteJanelas.Recarregar", e); _vigiadas = []; }
        Avisar();
    }

    private void AoMudarJanela(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ApplicationWindow.State) or nameof(ApplicationWindow.Title)
            or nameof(ApplicationWindow.Icon) or nameof(ApplicationWindow.IsMinimized) or nameof(ApplicationWindow.AppUserModelID))
            Avisar();
    }

    /// Junta as mudanças de uma rodada num aviso só.
    private void Avisar()
    {
        if (_avisoPendente) return;
        _avisoPendente = true;
        _ui.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _avisoPendente = false;
            Mudou?.Invoke();
        });
    }

    private void Com(IntPtr handle, Action<ApplicationWindow> acao, string onde)
    {
        try
        {
            var j = _vigiadas.FirstOrDefault(x => x.Handle == handle);
            if (j is not null) acao(j);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro(onde, e); }
    }

    public void Ativar(IntPtr janela) => Com(janela, j =>
    {
        if (j.IsMinimized) j.Restore();
        j.BringToFront();
    }, "FonteJanelas.Ativar");

    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool ShowWindow(IntPtr h, int comando);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetWindowPos(IntPtr h, IntPtr depoisDe, int x, int y, int l, int a, uint flags);
    private const int SwMostrarSemAtivar = 4; // SW_SHOWNOACTIVATE: desminimiza sem ativar
    private const uint SemMover = 0x2, SemTamanho = 0x1, SemAtivar = 0x10;
    private static readonly IntPtr AcimaDeTodas = -1, VoltaAoNormal = -2;

    /// Sobe a janela por cima das outras sem ativá-la. Durante um arraste o Windows não deixa outro programa tomar o
    /// foco (o Ativar não faria nada), e para soltar o arquivo basta a janela estar por cima. Põe e tira o "sempre
    /// visível": é o jeito que o Windows aceita de subir a janela de outro programa sem ser o da frente.
    public void TrazerSemFoco(IntPtr janela) => Com(janela, j =>
    {
        if (j.IsMinimized) ShowWindow(j.Handle, SwMostrarSemAtivar);
        SetWindowPos(j.Handle, AcimaDeTodas, 0, 0, 0, 0, SemMover | SemTamanho | SemAtivar);
        SetWindowPos(j.Handle, VoltaAoNormal, 0, 0, 0, 0, SemMover | SemTamanho | SemAtivar);
    }, "FonteJanelas.TrazerSemFoco");

    public void Minimizar(IntPtr janela) => Com(janela, j => j.Minimize(), "FonteJanelas.Minimizar");

    public void Fechar(IntPtr janela) => Com(janela, j => _bandeja.ServicoTarefas?.CloseWindow(j), "FonteJanelas.Fechar");

    public void Dispose()
    {
        _bandeja.Mudou -= Anexar;
        Desanexar();
    }
}

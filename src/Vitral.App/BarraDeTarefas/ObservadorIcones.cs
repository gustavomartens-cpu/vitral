using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Vitral.App.Barra;
using Vitral.Core.Barra;
using Vitral.Core.Nativo;
using Vitral.Core.Registro;

namespace Vitral.App.BarraDeTarefas;

/// Enquanto o Vitral está aberto, aplica a lista de ícones aplicada aos ícones que aparecem depois
/// (spec da barra de tarefas §5). Espera mudanças em HKCU\Control Panel\NotifyIconSettings numa thread própria.
/// Erro: registra no log e para; o app continua.
internal sealed partial class ObservadorIcones : IDisposable
{
    private const uint MudancaDeNome = 0x1, MudancaDeValor = 0x4;

    [LibraryImport("advapi32.dll")]
    private static partial int RegNotifyChangeKeyValue(SafeRegistryHandle chave, [MarshalAs(UnmanagedType.Bool)] bool subarvore,
        uint filtro, SafeWaitHandle evento, [MarshalAs(UnmanagedType.Bool)] bool assincrono);

    private readonly IRegistro _registro;
    private readonly EstadoIcones _estado;
    private readonly INotificador _notificador;
    private readonly ManualResetEvent _parar = new(false);
    private readonly Thread _thread;

    public ObservadorIcones(IRegistro registro, EstadoIcones estado, INotificador notificador)
    {
        _registro = registro;
        _estado = estado;
        _notificador = notificador;
        _thread = new Thread(Observar) { IsBackground = true, Name = "Vitral.ObservadorIcones" };
        _thread.Start();
    }

    private void Observar()
    {
        try
        {
            using var raiz = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var chave = raiz.OpenSubKey(Icones.Chave);
            if (chave is null) return;
            using var mudou = new AutoResetEvent(false);
            while (true)
            {
                int erro = RegNotifyChangeKeyValue(chave.Handle, true, MudancaDeNome | MudancaDeValor, mudou.SafeWaitHandle, true);
                if (erro != 0) throw new System.ComponentModel.Win32Exception(erro);
                if (WaitHandle.WaitAny([mudou, _parar]) == 1) return;
                if (_parar.WaitOne(500)) return; // o Windows grava os valores de um ícone novo em sequência
                if (Icones.AplicarNovos(_registro, _estado) > 0) _notificador.AvisarMudancaDaBarra();
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("ObservadorIcones", e);
        }
    }

    public void Dispose()
    {
        _parar.Set();
        _thread.Join(TimeSpan.FromSeconds(2));
        _parar.Dispose();
    }
}

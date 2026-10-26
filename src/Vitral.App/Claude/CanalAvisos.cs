using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Windows.Threading;
using Vitral.App.Barra;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// Lado do Vitral do canal dos avisos (spec do notch vivo §4.4): named pipe só do usuário atual, uma conexão por vez,
/// uma linha de no máximo 4 KB por aviso. Os avisos chegam na thread da interface.
internal sealed class CanalAvisos : IDisposable
{
    private readonly CancellationTokenSource _parar = new();

    public CanalAvisos(Dispatcher ui, Action<AvisoClaude> chegou)
    {
        var sid = WindowsIdentity.GetCurrent().User;
        if (sid is null) return;
        _ = Task.Run(() => Ouvir(Canal.Nome(sid.Value), sid, ui, chegou, _parar.Token));
    }

    private static async Task Ouvir(string nome, SecurityIdentifier sid, Dispatcher ui, Action<AvisoClaude> chegou, CancellationToken parar)
    {
        var seguranca = new PipeSecurity();
        seguranca.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        while (!parar.IsCancellationRequested)
        {
            try
            {
                await using var servidor = NamedPipeServerStreamAcl.Create(nome, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous, 4096, 0, seguranca); // buffer: uma linha pequena não espera o Vitral ler
                await servidor.WaitForConnectionAsync(parar);
                using var tempo = CancellationTokenSource.CreateLinkedTokenSource(parar);
                tempo.CancelAfter(TimeSpan.FromSeconds(2));
                var linha = await LerLinha(servidor, tempo.Token);
                if (AvisoClaude.DeLinha(linha) is { } aviso) _ = ui.BeginInvoke(chegou, aviso);
            }
            catch (OperationCanceledException) when (parar.IsCancellationRequested) { return; }
            catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException)
            {
                LogBarra.Erro("CanalAvisos", e);
                await Task.Delay(500, parar).ContinueWith(_ => { });
            }
        }
    }

    /// Até o "\n" ou o limite; linha maior que o limite volta inteira e o DeLinha a recusa.
    private static async Task<string> LerLinha(Stream s, CancellationToken ct)
    {
        var bytes = new byte[AvisoClaude.MaximoLinha * 4 + 1];
        int total = 0;
        while (total < bytes.Length)
        {
            int n = await s.ReadAsync(bytes.AsMemory(total), ct);
            if (n == 0) break;
            total += n;
            if (Array.IndexOf(bytes, (byte)'\n', 0, total) >= 0) break;
        }
        var texto = Encoding.UTF8.GetString(bytes, 0, total);
        int fim = texto.IndexOf('\n');
        return (fim >= 0 ? texto[..fim] : texto).TrimEnd('\r');
    }

    public void Dispose()
    {
        _parar.Cancel();
        _parar.Dispose();
    }
}

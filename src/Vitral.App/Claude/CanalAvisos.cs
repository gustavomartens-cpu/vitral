using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Windows.Threading;
using Vitral.App.Barra;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// Lado do Vitral do canal (spec do notch vivo §4.4; spec das permissões §3.5): named pipe só do usuário atual, até 4
/// conexões ao mesmo tempo, uma linha de no máximo 4 KB. Aviso: entrega e fecha. Pedido de permissão: entrega ao notch,
/// espera a escolha e responde na mesma conexão (ou fecha sem responder, se não houver escolha).
internal sealed class CanalAvisos : IDisposable
{
    private const int Instancias = 4;
    private readonly CancellationTokenSource _parar = new();

    public CanalAvisos(Dispatcher ui, Action<AvisoClaude> chegou, Func<PedidoPermissao, Task<EscolhaPermissao?>> chegouPedido)
    {
        var sid = WindowsIdentity.GetCurrent().User;
        if (sid is null) return;
        _ = Task.Run(() => Ouvir(Canal.Nome(sid.Value), sid, ui, chegou, chegouPedido, _parar.Token));
    }

    private static async Task Ouvir(string nome, SecurityIdentifier sid, Dispatcher ui, Action<AvisoClaude> chegou,
        Func<PedidoPermissao, Task<EscolhaPermissao?>> chegouPedido, CancellationToken parar)
    {
        var seguranca = new PipeSecurity();
        seguranca.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        while (!parar.IsCancellationRequested)
        {
            NamedPipeServerStream? servidor = null;
            try
            {
                servidor = NamedPipeServerStreamAcl.Create(nome, PipeDirection.InOut, Instancias, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous, 4096, 4096, seguranca); // buffers: uma linha pequena não espera o outro lado ler
                await servidor.WaitForConnectionAsync(parar);
                var conexao = servidor;
                servidor = null; // a conexão agora é do Atender; o laço já abre a próxima instância
                _ = Task.Run(() => Atender(conexao, ui, chegou, chegouPedido, parar));
            }
            catch (OperationCanceledException) when (parar.IsCancellationRequested) { servidor?.Dispose(); return; }
            catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException)
            {
                servidor?.Dispose();
                LogBarra.Erro("CanalAvisos", e); // inclui "todas as instâncias ocupadas" com 4 pedidos abertos
                await Task.Delay(500, parar).ContinueWith(_ => { });
            }
        }
    }

    private static async Task Atender(NamedPipeServerStream conexao, Dispatcher ui, Action<AvisoClaude> chegou,
        Func<PedidoPermissao, Task<EscolhaPermissao?>> chegouPedido, CancellationToken parar)
    {
        await using var posse = conexao; // fecha a conexão ao sair, com ou sem resposta
        try
        {
            using var tempo = CancellationTokenSource.CreateLinkedTokenSource(parar);
            tempo.CancelAfter(TimeSpan.FromSeconds(2));
            var linha = await LerLinha(conexao, tempo.Token);
            if (PedidoPermissao.DeLinha(linha) is { } pedido)
            {
                // O notch decide na thread da interface; o prazo do pedido (60 s) é dele, aqui só um limite de segurança.
                var escolha = await (await ui.InvokeAsync(() => chegouPedido(pedido))).WaitAsync(TimeSpan.FromSeconds(65), parar);
                if (escolha is not { } e) return;
                var bytes = new UTF8Encoding(false).GetBytes(RespostaPermissao.Linha(pedido.Id, e) + "\n");
                await conexao.WriteAsync(bytes, parar);
                await conexao.FlushAsync(parar);
            }
            else if (AvisoClaude.DeLinha(linha) is { } aviso) _ = ui.BeginInvoke(chegou, aviso);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or TimeoutException or ObjectDisposedException)
        {
            // o mensageiro desistiu ou o Vitral está fechando: nada a responder
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("CanalAvisos.Atender", e); }
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

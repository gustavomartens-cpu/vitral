using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Windows.Threading;
using Vitral.App.Barra;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// Lado do Vitral do canal (spec do notch vivo §4.4; spec das permissões §3.5): named pipe só do usuário atual, até 8
/// conexões ao mesmo tempo, uma linha de no máximo 4 KB. Aviso: entrega e fecha. Pedido de permissão: entrega ao notch,
/// espera a escolha e responde na mesma conexão (ou fecha sem responder, se não houver escolha). Se o mensageiro fechar
/// antes (o Claude largou o gancho: respondido no app), a ficha sai.
internal sealed class CanalAvisos : IDisposable
{
    private const int Instancias = 8, ErroCanalOcupado = 231; // ERROR_PIPE_BUSY
    private readonly CancellationTokenSource _parar = new();

    /// Quem atende cada tipo de linha, na thread da interface.
    private sealed record Destinos(Action<AvisoClaude> Chegou, Func<PedidoPermissao, Task<EscolhaPermissao?>> ChegouPedido,
        Func<PedidoResposta, Task<string?>> ChegouResposta, Action<string> Desistiu);

    public CanalAvisos(Dispatcher ui, Action<AvisoClaude> chegou, Func<PedidoPermissao, Task<EscolhaPermissao?>> chegouPedido,
        Func<PedidoResposta, Task<string?>> chegouResposta, Action<string> desistiu)
    {
        var sid = WindowsIdentity.GetCurrent().User;
        if (sid is null) return;
        var destinos = new Destinos(chegou, chegouPedido, chegouResposta, desistiu);
        _ = Task.Run(() => Ouvir(Canal.Nome(sid.Value), sid, ui, destinos, _parar.Token));
    }

    private static async Task Ouvir(string nome, SecurityIdentifier sid, Dispatcher ui, Destinos destinos, CancellationToken parar)
    {
        var seguranca = new PipeSecurity();
        seguranca.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        while (!parar.IsCancellationRequested)
        {
            NamedPipeServerStream? servidor = null;
            try
            {
                servidor = NamedPipeServerStreamAcl.Create(nome, PipeDirection.InOut, Instancias, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous, AvisoClaude.MaximoLinha, AvisoClaude.MaximoLinha, seguranca); // uma linha inteira não espera o outro lado ler
                await servidor.WaitForConnectionAsync(parar);
                var conexao = servidor;
                servidor = null; // a conexão agora é do Atender; o laço já abre a próxima instância
                _ = Task.Run(() => Atender(conexao, ui, destinos, parar));
            }
            catch (OperationCanceledException) when (parar.IsCancellationRequested) { servidor?.Dispose(); return; }
            catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException)
            {
                servidor?.Dispose();
                // Todas as instâncias ocupadas (8 pedidos abertos) é esperado: tenta de novo sem encher o log.
                if ((e.HResult & 0xFFFF) != ErroCanalOcupado) LogBarra.Erro("CanalAvisos", e);
                await Task.Delay(500, parar).ContinueWith(_ => { });
            }
        }
    }

    private static async Task Atender(NamedPipeServerStream conexao, Dispatcher ui, Destinos destinos, CancellationToken parar)
    {
        await using var posse = conexao; // fecha a conexão ao sair, com ou sem resposta
        try
        {
            using var tempo = CancellationTokenSource.CreateLinkedTokenSource(parar);
            tempo.CancelAfter(TimeSpan.FromSeconds(2));
            var linha = await LerLinha(conexao, tempo.Token);
            // Pedido de permissão (prazo do notch 60 s) e ficha de resposta (60 s, ou 15 min digitando): o notch decide;
            // os limites aqui são só de segurança.
            if (PedidoPermissao.DeLinha(linha) is { } pedido)
            {
                if (await Decidir(conexao, ui, () => destinos.ChegouPedido(pedido), pedido.Id, destinos.Desistiu, TimeSpan.FromSeconds(65), parar) is { } e)
                    await Responder(conexao, RespostaPermissao.Linha(pedido.Id, e), parar);
            }
            else if (PedidoResposta.DeLinha(linha) is { } ficha)
            {
                if (await Decidir(conexao, ui, () => destinos.ChegouResposta(ficha), ficha.Id, destinos.Desistiu, TimeSpan.FromSeconds(1170), parar) is { } texto)
                    await Responder(conexao, RespostaTexto.Linha(ficha.Id, texto), parar);
            }
            else if (AvisoClaude.DeLinha(linha) is { } aviso) _ = ui.BeginInvoke(destinos.Chegou, aviso);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or TimeoutException or ObjectDisposedException)
        {
            // o mensageiro desistiu ou o Vitral está fechando: nada a responder
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("CanalAvisos.Atender", e); }
    }

    /// Entrega ao notch e espera a decisão; se o mensageiro fechar antes (o Claude largou o pedido), avisa o notch e
    /// devolve o padrão (nada a responder).
    private static async Task<T?> Decidir<T>(Stream conexao, Dispatcher ui, Func<Task<T?>> entregar, string id, Action<string> desistiu,
        TimeSpan limite, CancellationToken parar)
    {
        var decisao = await ui.InvokeAsync(entregar);
        var fechou = EsperarFechar(conexao, parar);
        if (await Task.WhenAny(decisao, fechou).WaitAsync(limite, parar) == fechou)
        {
            _ = ui.BeginInvoke(desistiu, id);
            return default;
        }
        return await decisao;
    }

    private static async Task Responder(Stream conexao, string linha, CancellationToken parar)
    {
        var bytes = new UTF8Encoding(false).GetBytes(linha + "\n");
        await conexao.WriteAsync(bytes, parar);
        await conexao.FlushAsync(parar);
    }

    /// Termina quando o outro lado fecha a conexão (o mensageiro não escreve nada depois do pedido).
    private static async Task EsperarFechar(Stream s, CancellationToken ct)
    {
        try
        {
            var b = new byte[1];
            while (await s.ReadAsync(b, ct) > 0) { }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
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

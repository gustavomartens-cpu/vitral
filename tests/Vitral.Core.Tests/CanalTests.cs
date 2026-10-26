using System.Diagnostics;
using System.IO.Pipes;
using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

public sealed class CanalTests
{
    [Fact]
    public void Nome_do_canal_tem_o_sid() => Assert.Equal("Vitral-Avisos-S-1-5-21-1", Canal.Nome("S-1-5-21-1"));

    [Fact]
    public async Task Enviar_chega_ao_servidor()
    {
        var nome = "Vitral-Teste-" + Guid.NewGuid().ToString("N");
        using var servidor = new NamedPipeServerStream(nome, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var conexao = servidor.WaitForConnectionAsync();
        var envio = Task.Run(() => Canal.Enviar(nome, "{\"tipo\":\"Terminou\"}"));
        await conexao.WaitAsync(TimeSpan.FromSeconds(5));
        using var leitor = new StreamReader(servidor);
        Assert.Equal("{\"tipo\":\"Terminou\"}", await leitor.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await envio);
    }

    [Fact]
    public void Sem_servidor_sai_rapido()
    {
        var relogio = Stopwatch.StartNew();
        Assert.False(Canal.Enviar("Vitral-Inexistente-" + Guid.NewGuid().ToString("N"), "x"));
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(1), $"demorou {relogio.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Servidor_que_nao_le_nao_prende_o_envio()
    {
        var nome = "Vitral-Teste-" + Guid.NewGuid().ToString("N");
        using var servidor = new NamedPipeServerStream(nome, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0);
        _ = servidor.WaitForConnectionAsync(); // aceita a conexão e nunca lê (Vitral congelado)
        var relogio = Stopwatch.StartNew();
        Canal.Enviar(nome, new string((char)0x61, 64 * 1024));
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(2), $"demorou {relogio.ElapsedMilliseconds} ms");
    }
}

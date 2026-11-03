using Vitral.Core.Controles;

namespace Vitral.Core.Tests;

public sealed class FilaUltimoValorTests
{
    [Fact]
    public async Task Escreve_um_de_cada_vez_e_so_o_ultimo_que_esperou()
    {
        // O DDC/CI é lento: um brilho arrastado não pode ter escritas sobrepostas nem chegar fora de ordem (revisão, I1).
        var liberar = new SemaphoreSlim(0);
        var escritos = new List<int>();
        int simultaneas = 0, maximo = 0;
        var fila = new FilaUltimoValor<string, int>((_, v) =>
        {
            maximo = Math.Max(maximo, Interlocked.Increment(ref simultaneas));
            if (v == 10) liberar.Wait();
            lock (escritos) escritos.Add(v);
            Interlocked.Decrement(ref simultaneas);
        });
        fila.Pedir("ddc1", 10);
        await Task.Delay(50);
        fila.Pedir("ddc1", 20);
        fila.Pedir("ddc1", 30);
        liberar.Release();
        await fila.Esperar();
        Assert.Equal([10, 30], escritos);
        Assert.Equal(1, maximo);
    }

    [Fact]
    public async Task Erro_na_escrita_nao_trava_a_fila()
    {
        var escritos = new List<int>();
        var fila = new FilaUltimoValor<string, int>((_, v) =>
        {
            if (v == 1) throw new InvalidOperationException("monitor sumiu");
            lock (escritos) escritos.Add(v);
        });
        fila.Pedir("ddc1", 1);
        await fila.Esperar();
        fila.Pedir("ddc1", 2);
        await fila.Esperar();
        Assert.Equal([2], escritos);
    }
}

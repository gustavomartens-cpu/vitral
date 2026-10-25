using Vitral.Core.Nativo;

namespace Vitral.Core.Tests;

public class NotificadorEmSegundoPlanoTests
{
    private sealed class Gravador : INotificador
    {
        public readonly List<(string Aviso, int Thread)> Chamadas = [];
        public readonly ManualResetEventSlim Liberar = new(true);
        public void AvisarMudancaDeTema() { Liberar.Wait(); lock (Chamadas) Chamadas.Add(("tema", Environment.CurrentManagedThreadId)); }
        public void AvisarMudancaDaBarra() { Liberar.Wait(); lock (Chamadas) Chamadas.Add(("barra", Environment.CurrentManagedThreadId)); }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }

    [Fact]
    public void Avisa_fora_da_thread_de_quem_chamou()
    {
        var g = new Gravador();
        var n = new NotificadorEmSegundoPlano(g);
        n.AvisarMudancaDeTema();
        Assert.True(n.Aguardar(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(Environment.CurrentManagedThreadId, Assert.Single(g.Chamadas).Thread);
    }

    [Fact]
    public void Nao_bloqueia_quem_chamou()
    {
        var g = new Gravador();
        g.Liberar.Reset(); // o aviso "trava" até liberarmos
        var n = new NotificadorEmSegundoPlano(g);

        n.AvisarMudancaDeTema(); // tem de voltar na hora

        Assert.Empty(g.Chamadas);
        g.Liberar.Set();
        Assert.True(n.Aguardar(TimeSpan.FromSeconds(5)));
        Assert.Single(g.Chamadas);
    }

    [Fact]
    public void Mantem_a_ordem_dos_avisos()
    {
        var g = new Gravador();
        var n = new NotificadorEmSegundoPlano(g);
        n.AvisarMudancaDeTema();
        n.AvisarMudancaDaBarra();
        n.AvisarMudancaDeTema();
        Assert.True(n.Aguardar(TimeSpan.FromSeconds(5)));
        Assert.Equal(["tema", "barra", "tema"], g.Chamadas.Select(c => c.Aviso));
    }
}

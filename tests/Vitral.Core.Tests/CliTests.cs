using Vitral.Cli;

namespace Vitral.Core.Tests;

public class CliTests
{
    [Fact]
    public void Testar_com_segundos()
    {
        Assert.Equal(new Comando.Testar("Vidro escuro", 5), Comando.Interpretar(["testar", "Vidro escuro", "--segundos", "5"]));
    }

    [Fact]
    public void Testar_sem_segundos_usa_20()
    {
        Assert.Equal(new Comando.Testar("x", 20), Comando.Interpretar(["testar", "x"]));
    }

    [Fact]
    public void Sem_argumentos_e_uso()
    {
        Assert.IsType<Comando.Uso>(Comando.Interpretar([]));
    }

    [Fact]
    public void Comando_desconhecido_e_uso()
    {
        Assert.IsType<Comando.Uso>(Comando.Interpretar(["voar"]));
    }

    [Fact]
    public void Aplicar_sem_nome_e_uso()
    {
        Assert.IsType<Comando.Uso>(Comando.Interpretar(["aplicar"]));
    }

    [Fact]
    public void Exportar_com_varios_perfis()
    {
        var c = Assert.IsType<Comando.Exportar>(Comando.Interpretar(["exportar", "meus.vitral", "A", "B"]));
        Assert.Equal("meus.vitral", c.Arquivo);
        Assert.Equal(["A", "B"], c.Nomes);
    }

    [Theory]
    [InlineData("retrato")]
    [InlineData("desfazer")]
    [InlineData("original")]
    public void Comandos_sem_argumento(string nome)
    {
        Assert.IsNotType<Comando.Uso>(Comando.Interpretar([nome]));
    }

    [Fact]
    public void Segundos_invalido_e_uso()
    {
        Assert.IsType<Comando.Uso>(Comando.Interpretar(["testar", "x", "--segundos", "abc"]));
    }
}

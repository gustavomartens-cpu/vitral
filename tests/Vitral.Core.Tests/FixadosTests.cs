using Vitral.Core.Doca;

namespace Vitral.Core.Tests;

public sealed class FixadosTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-fixados-" + Guid.NewGuid().ToString("N"));
    private string Arquivo => Path.Combine(_raiz, "fixados.json");

    [Fact]
    public void Ida_e_volta()
    {
        var lista = new List<Fixado>
        {
            new(@"C:\l\Steam.lnk", null, "Steam", @"C:\Steam\steam.exe"),
            new(@"C:\l\Spotify.lnk", "Spotify", "Spotify"),
        };
        new RepositorioFixados(Arquivo).Gravar(lista);
        Assert.Equal(lista, new RepositorioFixados(Arquivo).Ler());
    }

    [Fact]
    public void Sem_arquivo_e_null() => Assert.Null(new RepositorioFixados(Arquivo).Ler());

    [Fact]
    public void Json_corrompido_e_null()
    {
        Directory.CreateDirectory(_raiz);
        File.WriteAllText(Arquivo, "{ isto não é json");
        Assert.Null(new RepositorioFixados(Arquivo).Ler());
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }
}

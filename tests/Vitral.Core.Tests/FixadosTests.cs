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

public sealed class AlvoDoFixadoTests
{
    private static Func<string, bool> Existem(params string[] caminhos) => c => caminhos.Contains(c, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Atalho_que_ficou_sem_alvo_nao_existe() =>
        Assert.False(new Fixado(@"C:\l\X.lnk", "X", "X", @"C:\x\x.exe").Existe(Existem(@"C:\l\X.lnk")));

    [Fact]
    public void Atalho_com_alvo_existe() =>
        Assert.True(new Fixado(@"C:\l\X.lnk", "X", "X", @"C:\x\x.exe").Existe(Existem(@"C:\l\X.lnk", @"C:\x\x.exe")));

    [Fact]
    public void Sem_alvo_conhecido_vale_o_atalho() =>
        Assert.True(new Fixado(@"C:\l\Explorer.lnk", "E", "E").Existe(Existem(@"C:\l\Explorer.lnk")));

    [Fact]
    public void App_da_loja_pelo_shell_existe() =>
        Assert.True(new Fixado(@"shell:AppsFolder\Calc!App", "Calc!App", "Calc").Existe(Existem()));
}

using System.IO.Compression;
using Vitral.Core.Perfis;

namespace Vitral.Core.Tests;

public sealed class PacoteTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-testes-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private static Perfil Novo(string nome, string cor) =>
        new(1, nome, "26200.9550", new AjustesNativosPerfil(Modo.Escuro, null, cor, null, true, null, null, null));

    [Fact]
    public void Exportar_e_importar_em_outra_pasta_preserva_os_perfis_sem_caminho_absoluto()
    {
        var a = new RepositorioPerfis(Path.Combine(_raiz, "A"));
        a.Salvar(Novo("Vidro escuro", "#21375E"));
        a.Salvar(Novo("Vermelho", "#E81123"));
        string zip = Path.Combine(_raiz, "meus.vitral");

        Pacote.Exportar(a, ["Vidro escuro", "Vermelho"], zip, "0.1.0", "26200.9550");

        using (var arquivo = ZipFile.OpenRead(zip))
            Assert.All(arquivo.Entries, e =>
            {
                Assert.DoesNotContain(':', e.FullName);
                Assert.False(e.FullName.StartsWith('\\') || e.FullName.StartsWith('/'), e.FullName);
            });

        var b = new RepositorioPerfis(Path.Combine(_raiz, "B"));
        var importados = Pacote.Importar(zip, b);

        Assert.Equal(2, importados.Count);
        Assert.Equal(a.Carregar("Vidro escuro"), b.Carregar("Vidro escuro"));
        Assert.Equal(a.Carregar("Vermelho"), b.Carregar("Vermelho"));
    }

    [Fact]
    public void Zip_sem_manifest_e_recusado()
    {
        string zip = Path.Combine(_raiz, "ruim.vitral");
        Directory.CreateDirectory(_raiz);
        using (var arquivo = ZipFile.Open(zip, ZipArchiveMode.Create)) arquivo.CreateEntry("qualquer.txt");
        Assert.Throws<PerfilInvalidoException>(() => Pacote.Importar(zip, new RepositorioPerfis(Path.Combine(_raiz, "B"))));
    }

    [Fact]
    public void Manifest_de_esquema_futuro_e_recusado_sem_gravar_nada()
    {
        string zip = Path.Combine(_raiz, "futuro.vitral");
        Directory.CreateDirectory(_raiz);
        using (var arquivo = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using var w = new StreamWriter(arquivo.CreateEntry("manifest.json").Open());
            w.Write("""{ "esquema": 2, "versaoApp": "9.0", "buildOrigem": "x", "perfis": ["X"] }""");
        }
        string destino = Path.Combine(_raiz, "B");
        Assert.Throws<PerfilInvalidoException>(() => Pacote.Importar(zip, new RepositorioPerfis(destino)));
        Assert.False(Directory.Exists(destino) && Directory.EnumerateFileSystemEntries(destino).Any());
    }

    [Fact]
    public void Nome_com_barra_vira_pasta_valida()
    {
        var repo = new RepositorioPerfis(Path.Combine(_raiz, "A"));
        repo.Salvar(Novo("Vidro/escuro", "#21375E"));
        Assert.True(File.Exists(Path.Combine(_raiz, "A", "Vidro_escuro", "perfil.json")));
        Assert.Equal("Vidro/escuro", repo.Carregar("Vidro/escuro").Nome);
        Assert.Contains("Vidro/escuro", repo.Nomes());
    }
}

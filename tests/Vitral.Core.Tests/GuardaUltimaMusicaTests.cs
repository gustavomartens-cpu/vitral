using Vitral.Core.Barra;

namespace Vitral.Core.Tests;

public sealed class GuardaUltimaMusicaTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-musica-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    [Fact]
    public void Sem_arquivo_le_nada()
    {
        Assert.Null(new GuardaUltimaMusica(_raiz).Ler());
    }

    [Fact]
    public void Grava_e_le_com_capa()
    {
        var guarda = new GuardaUltimaMusica(_raiz);
        guarda.Gravar(new UltimaMusica("Blinding Lights", "The Weeknd", [1, 2, 3], Escondida: true));
        var lida = new GuardaUltimaMusica(_raiz).Ler();
        Assert.NotNull(lida);
        Assert.Equal("Blinding Lights", lida.Titulo);
        Assert.Equal("The Weeknd", lida.Artista);
        Assert.Equal([1, 2, 3], lida.Capa);
        Assert.True(lida.Escondida);
    }

    [Fact]
    public void Sem_capa_apaga_a_capa_velha()
    {
        var guarda = new GuardaUltimaMusica(_raiz);
        guarda.Gravar(new UltimaMusica("A", null, [9], false));
        guarda.Gravar(new UltimaMusica("B", null, null, false));
        var lida = guarda.Ler();
        Assert.Equal("B", lida!.Titulo);
        Assert.Null(lida.Capa);
    }

    [Fact]
    public void Json_quebrado_le_nada()
    {
        Directory.CreateDirectory(_raiz);
        File.WriteAllText(Path.Combine(_raiz, GuardaUltimaMusica.ArquivoJson), "{ quebrado");
        Assert.Null(new GuardaUltimaMusica(_raiz).Ler());
    }

    [Fact]
    public void Sem_titulo_le_nada()
    {
        Directory.CreateDirectory(_raiz);
        File.WriteAllText(Path.Combine(_raiz, GuardaUltimaMusica.ArquivoJson), "{\"titulo\":\"\"}");
        Assert.Null(new GuardaUltimaMusica(_raiz).Ler());
    }
}

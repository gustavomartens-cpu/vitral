using System.Text.Json;

namespace Vitral.Core.Barra;

/// A última música do Spotify (spec da prévia do Spotify §2). Escondida: o × foi clicado e o Spotify não tocou desde então.
public sealed record UltimaMusica(string Titulo, string? Artista, byte[]? Capa, bool Escondida);

public interface IGuardaUltimaMusica
{
    /// Null sem música guardada ou com o arquivo ilegível.
    UltimaMusica? Ler();
    void Gravar(UltimaMusica musica);
}

/// Guarda em %LOCALAPPDATA%\Vitral: o JSON com título, artista e o ×, e a capa num arquivo à parte. Nunca lança.
public sealed class GuardaUltimaMusica(string pasta) : IGuardaUltimaMusica
{
    public const string ArquivoJson = "ultima-musica.json";
    public const string ArquivoCapa = "ultima-capa.bin";

    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed record Dados(string? Titulo, string? Artista, bool Escondida);

    private string CaminhoJson => Path.Combine(pasta, ArquivoJson);
    private string CaminhoCapa => Path.Combine(pasta, ArquivoCapa);

    public UltimaMusica? Ler()
    {
        try
        {
            if (!File.Exists(CaminhoJson)) return null;
            var d = JsonSerializer.Deserialize<Dados>(File.ReadAllText(CaminhoJson), Opcoes);
            if (d is null || string.IsNullOrWhiteSpace(d.Titulo)) return null;
            byte[]? capa = File.Exists(CaminhoCapa) ? File.ReadAllBytes(CaminhoCapa) : null;
            return new UltimaMusica(d.Titulo, d.Artista, capa is { Length: > 0 } ? capa : null, d.Escondida);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public void Gravar(UltimaMusica musica)
    {
        try
        {
            Directory.CreateDirectory(pasta);
            // Cada arquivo vai para um .tmp e é trocado de uma vez: uma queda no meio não deixa o JSON truncado.
            if (musica.Capa is { Length: > 0 } capa) Trocar(CaminhoCapa, t => File.WriteAllBytes(t, capa));
            else if (File.Exists(CaminhoCapa)) File.Delete(CaminhoCapa);
            var json = JsonSerializer.Serialize(new Dados(musica.Titulo, musica.Artista, musica.Escondida), Opcoes);
            Trocar(CaminhoJson, t => File.WriteAllText(t, json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // a prévia é opcional
    }

    private static void Trocar(string destino, Action<string> escrever)
    {
        string tmp = destino + ".tmp";
        escrever(tmp);
        File.Move(tmp, destino, overwrite: true);
    }
}

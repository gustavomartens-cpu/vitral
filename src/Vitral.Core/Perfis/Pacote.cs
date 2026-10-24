using System.IO.Compression;
using System.Text.Json;

namespace Vitral.Core.Perfis;

/// Arquivo .vitral: zip com manifest.json e perfis/<pasta>/perfil.json. Sem caminhos da máquina (spec §4.4).
public static class Pacote
{
    private const int EsquemaManifest = 1;

    private sealed record Manifest(int Esquema, string VersaoApp, string BuildOrigem, List<string> Perfis);

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Exportar(RepositorioPerfis repo, IEnumerable<string> nomes, string destinoZip, string versaoApp, string build)
    {
        var perfis = nomes.Select(repo.Carregar).ToList(); // falha antes de criar o zip se algum não existir
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinoZip))!);
        if (File.Exists(destinoZip)) File.Delete(destinoZip);

        using var zip = ZipFile.Open(destinoZip, ZipArchiveMode.Create);
        Escrever(zip, "manifest.json",
            JsonSerializer.Serialize(new Manifest(EsquemaManifest, versaoApp, build, perfis.Select(p => p.Nome).ToList()), Opcoes));
        foreach (var p in perfis)
            Escrever(zip, $"perfis/{RepositorioPerfis.NomeDePasta(p.Nome)}/perfil.json", PerfilJson.Escrever(p));
    }

    /// Valida tudo antes de gravar qualquer coisa no destino.
    public static IReadOnlyList<Perfil> Importar(string origemZip, RepositorioPerfis destino)
    {
        List<Perfil> perfis;
        using (var zip = ZipFile.OpenRead(origemZip))
        {
            var entradaManifest = zip.GetEntry("manifest.json")
                ?? throw new PerfilInvalidoException("Arquivo .vitral sem manifest.json.");
            Manifest? m;
            try { m = JsonSerializer.Deserialize<Manifest>(Ler(entradaManifest), Opcoes); }
            catch (JsonException e) { throw new PerfilInvalidoException($"manifest.json inválido: {e.Message}", e); }

            if (m is null) throw new PerfilInvalidoException("manifest.json vazio.");
            if (m.Esquema < 1 || m.Esquema > EsquemaManifest)
                throw new PerfilInvalidoException(
                    $"Este arquivo .vitral usa o esquema {m.Esquema}; esta versão do Vitral entende até o {EsquemaManifest}.");

            perfis = (m.Perfis ?? []).Select(nome =>
            {
                var entrada = zip.GetEntry($"perfis/{RepositorioPerfis.NomeDePasta(nome)}/perfil.json")
                    ?? throw new PerfilInvalidoException($"O manifest cita o perfil \"{nome}\", mas ele não está no arquivo.");
                return PerfilJson.Ler(Ler(entrada));
            }).ToList();
        }

        foreach (var p in perfis) destino.Salvar(p);
        return perfis;
    }

    private static void Escrever(ZipArchive zip, string caminho, string conteudo)
    {
        using var w = new StreamWriter(zip.CreateEntry(caminho).Open());
        w.Write(conteudo);
    }

    private static string Ler(ZipArchiveEntry entrada)
    {
        using var r = new StreamReader(entrada.Open());
        return r.ReadToEnd();
    }
}

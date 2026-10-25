using System.Text.Json;

namespace Vitral.Core.Doca;

/// O fixados.json da doca. Arquivo ausente ou corrompido devolve null (quem chama decide importar de novo).
public sealed class RepositorioFixados(string arquivo)
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public IReadOnlyList<Fixado>? Ler()
    {
        try
        {
            return File.Exists(arquivo) ? JsonSerializer.Deserialize<List<Fixado>>(File.ReadAllText(arquivo), Opcoes) : null;
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return null;
        }
    }

    /// Grava num temporário e troca, para uma queda no meio não deixar o arquivo pela metade.
    public void Gravar(IReadOnlyList<Fixado> lista)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arquivo))!);
        string tmp = arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(lista, Opcoes));
        File.Move(tmp, arquivo, overwrite: true);
    }
}

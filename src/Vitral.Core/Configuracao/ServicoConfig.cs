using System.Text.Json;

namespace Vitral.Core.Configuracao;

/// Lê e grava o config.json. Arquivo ausente ou corrompido vira o padrão (o app nunca deixa de abrir por isso).
public sealed class ServicoConfig(string arquivo)
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public ConfigApp Ler()
    {
        try
        {
            return File.Exists(arquivo)
                ? JsonSerializer.Deserialize<ConfigApp>(File.ReadAllText(arquivo), Opcoes) ?? ConfigApp.Padrao
                : ConfigApp.Padrao;
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return ConfigApp.Padrao;
        }
    }

    /// Grava num temporário e troca, para uma queda no meio não deixar o arquivo pela metade.
    public void Gravar(ConfigApp c)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arquivo))!);
        string tmp = arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(c, Opcoes));
        File.Move(tmp, arquivo, overwrite: true);
    }
}

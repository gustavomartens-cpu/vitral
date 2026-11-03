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
        lock (Trava) return LerSemTrava();
    }

    /// Lê, muda e grava de uma vez, sem outra thread no meio (o modo avião grava fora da thread da interface).
    public void Atualizar(Func<ConfigApp, ConfigApp> mudar)
    {
        lock (Trava) GravarSemTrava(mudar(LerSemTrava()));
    }

    /// Uma trava para todas as instâncias: o app tem um config.json só, aberto por vários serviços.
    private static readonly object Trava = new();

    private ConfigApp LerSemTrava()
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
        lock (Trava) GravarSemTrava(c);
    }

    private void GravarSemTrava(ConfigApp c)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arquivo))!);
        string tmp = arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(c, Opcoes));
        File.Move(tmp, arquivo, overwrite: true);
    }
}

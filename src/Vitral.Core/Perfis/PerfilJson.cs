using System.Text.Json;
using System.Text.Json.Serialization;
using Vitral.Core.Cores;

namespace Vitral.Core.Perfis;

/// Lê e grava o perfil.json (camelCase, enums em texto minúsculo). Esquemas 1 e 2 são lidos normalmente (sem as seções novas);
/// a gravação é sempre no esquema atual. Seções desconhecidas são ignoradas na leitura.
public static class PerfilJson
{
    public const int EsquemaSuportado = 3;

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Perfil Ler(string json)
    {
        // O esquema é conferido antes do resto: um perfil de versão futura pode ter campos que esta versão não entende.
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("esquema", out var e) && e.TryGetInt32(out int esquema) &&
                esquema > EsquemaSuportado)
                throw new PerfilInvalidoException(
                    $"Este perfil usa o esquema {esquema}, mas esta versão do Vitral entende até o esquema {EsquemaSuportado}.");
        }
        catch (JsonException e)
        {
            throw new PerfilInvalidoException($"perfil.json inválido: {e.Message}", e);
        }

        Perfil? p;
        try
        {
            p = JsonSerializer.Deserialize<Perfil>(json, Opcoes);
        }
        catch (JsonException e)
        {
            throw new PerfilInvalidoException($"perfil.json inválido: {e.Message}", e);
        }

        if (p is null) throw new PerfilInvalidoException("perfil.json vazio.");
        if (p.Esquema < 1 || p.Esquema > EsquemaSuportado)
            throw new PerfilInvalidoException(
                $"Este perfil usa o esquema {p.Esquema}, mas esta versão do Vitral entende até o esquema {EsquemaSuportado}.");
        if (string.IsNullOrWhiteSpace(p.Nome)) throw new PerfilInvalidoException("O perfil precisa de um nome.");

        if (p.Nativo?.CorDestaque is { } cor)
        {
            try { Cor.DeHex(cor); }
            catch (FormatException e) { throw new PerfilInvalidoException($"corDestaque: {e.Message}", e); }
        }
        return p;
    }

    public static string Escrever(Perfil p) => JsonSerializer.Serialize(p with { Esquema = EsquemaSuportado }, Opcoes);
}

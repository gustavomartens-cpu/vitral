using System.Reflection;

namespace Vitral.Core.Atualizacao;

/// O token de leitura das releases, embutido pelo build (spec da atualização §3): vem da variável
/// VITRAL_TOKEN_ATUALIZACAO, vira AssemblyMetadata("TokenAtualizacao") e nunca passa pelo git. Ausente ou vazio: null.
public static class TokenEmbutido
{
    public static string? Ler(Assembly assembly, string chave = "TokenAtualizacao") =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == chave)?.Value is { Length: > 0 } valor
            ? valor : null;
}

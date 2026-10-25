namespace Vitral.Core.Doca;

/// Um app fixado na doca: o atalho (ou executável), o AppUserModelID se houver, o nome e o executável alvo do atalho.
public sealed record Fixado(string Caminho, string? AppId, string Nome, string? Alvo = null)
{
    /// O app ainda está lá: com alvo conhecido vale o alvo (desinstalar deixa o .lnk para trás); sem alvo, o atalho;
    /// app da loja pelo shell: sempre.
    public bool Existe(Func<string, bool> arquivoExiste) =>
        Caminho.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
        || (Alvo is { } alvo ? arquivoExiste(alvo) : arquivoExiste(Caminho));
}

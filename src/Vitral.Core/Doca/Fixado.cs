namespace Vitral.Core.Doca;

/// Um app fixado na doca: o atalho (ou executável), o AppUserModelID se houver, o nome e o executável alvo do atalho.
public sealed record Fixado(string Caminho, string? AppId, string Nome, string? Alvo = null);

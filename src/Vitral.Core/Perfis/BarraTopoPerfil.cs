namespace Vitral.Core.Perfis;

/// Ilhas da barra do topo. Campo ausente no JSON vale ligado; <see cref="Desempenho"/> é CPU e MEM na ilha de status.
public sealed record IlhasBarra(bool Midia = true, bool Relogio = true, bool Status = true, bool Desempenho = true)
{
    public static IlhasBarra Todas { get; } = new();
}

/// Seção "barraTopo" do perfil (esquema 2). A seção ausente ou nula no perfil significa "não mexer".
public sealed record BarraTopoPerfil(bool Visivel, IlhasBarra? Ilhas = null)
{
    public static BarraTopoPerfil Desligada { get; } = new(false);

    public IlhasBarra IlhasEfetivas => Ilhas ?? IlhasBarra.Todas;
}

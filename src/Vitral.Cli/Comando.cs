namespace Vitral.Cli;

/// Comandos do `vitral` (plano 1a, Tarefa 6). Interpretar não toca no Windows: é testável.
public abstract record Comando
{
    public sealed record Uso : Comando;
    public sealed record Retrato : Comando;
    public sealed record SalvarAtual(string Nome) : Comando;
    public sealed record Aplicar(string Nome) : Comando;
    public sealed record Testar(string Nome, int Segundos) : Comando;
    public sealed record Desfazer : Comando;
    public sealed record Original : Comando;
    public sealed record Exportar(string Arquivo, IReadOnlyList<string> Nomes) : Comando
    {
        public bool Equals(Exportar? o) => o is not null && Arquivo == o.Arquivo && Nomes.SequenceEqual(o.Nomes);
        public override int GetHashCode() => Arquivo.GetHashCode();
    }
    public sealed record Importar(string Arquivo, bool Substituir) : Comando;

    /// Teto do "testar": acima disso a espera não faz sentido (e o Task.Wait não aceita prazos muito longos).
    public const int SegundosMaximos = 600;

    public const string TextoDeUso = """
        Uso: vitral <comando>
          retrato                         mostra os valores atuais que o Vitral controla
          salvar-atual <nome>             cria um perfil com o estado atual
          aplicar <nome>                  aplica um perfil
          testar <nome> [--segundos N]    aplica e volta sozinho se você não confirmar (padrão 20 s)
          desfazer                        volta ao estado antes da última mudança
          original                        volta ao Windows como estava antes do primeiro uso do Vitral
          exportar <arquivo.vitral> <nome...>
          importar <arquivo.vitral> [--substituir]
        """;

    public static Comando Interpretar(IReadOnlyList<string> a)
    {
        if (a.Count == 0) return new Uso();
        return (a[0].ToLowerInvariant(), a.Count) switch
        {
            ("retrato", 1) => new Retrato(),
            ("desfazer", 1) => new Desfazer(),
            ("original", 1) => new Original(),
            ("salvar-atual", 2) => new SalvarAtual(a[1]),
            ("aplicar", 2) => new Aplicar(a[1]),
            ("importar", 2) => new Importar(a[1], false),
            ("importar", 3) when a[2] == "--substituir" => new Importar(a[1], true),
            ("exportar", >= 3) => new Exportar(a[1], a.Skip(2).ToList()),
            ("testar", 2) => new Testar(a[1], 20),
            ("testar", 4) when a[2] == "--segundos" && int.TryParse(a[3], out int s) && s is > 0 and <= SegundosMaximos => new Testar(a[1], s),
            _ => new Uso(),
        };
    }
}

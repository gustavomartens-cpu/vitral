namespace Vitral.Core.Perfis;

/// Perfis em disco: <pasta>\<nome do perfil, sem caracteres inválidos>\perfil.json.
public sealed class RepositorioPerfis(string pastaPerfis)
{
    public string Pasta { get; } = pastaPerfis;

    public IReadOnlyList<string> Nomes() =>
        Directory.Exists(Pasta)
            ? Directory.GetDirectories(Pasta)
                .Select(d => Path.Combine(d, "perfil.json"))
                .Where(File.Exists)
                .Select(f => PerfilJson.Ler(File.ReadAllText(f)).Nome)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : [];

    public Perfil Carregar(string nome)
    {
        string arquivo = ArquivoDe(nome);
        if (!File.Exists(arquivo)) throw new FileNotFoundException($"Perfil \"{nome}\" não encontrado.", arquivo);
        return PerfilJson.Ler(File.ReadAllText(arquivo));
    }

    public void Salvar(Perfil p)
    {
        string arquivo = ArquivoDe(p.Nome);
        Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
        File.WriteAllText(arquivo, PerfilJson.Escrever(p));
    }

    /// Nome da pasta de um perfil: caracteres inválidos em nome de arquivo viram "_".
    public static string NomeDePasta(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        return new string(nome.Trim().Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
    }

    private string ArquivoDe(string nome) => Path.Combine(Pasta, NomeDePasta(nome), "perfil.json");
}

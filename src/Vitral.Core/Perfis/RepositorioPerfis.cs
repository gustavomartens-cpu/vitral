namespace Vitral.Core.Perfis;

/// Perfis em disco: <pasta>\<nome do perfil, sem caracteres inválidos>\perfil.json.
public sealed class RepositorioPerfis(string pastaPerfis)
{
    public string Pasta { get; } = pastaPerfis;

    /// Nomes dos perfis válidos. Um perfil.json ilegível é ignorado aqui (não pode impedir o app de abrir);
    /// ao tentar carregá-lo, <see cref="Carregar"/> mostra o erro.
    public IReadOnlyList<string> Nomes() =>
        Directory.Exists(Pasta)
            ? Directory.GetDirectories(Pasta)
                .Select(d => Path.Combine(d, "perfil.json"))
                .Where(File.Exists)
                .Select(NomeSeValido)
                .OfType<string>()
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : [];

    private static string? NomeSeValido(string arquivo)
    {
        try { return PerfilJson.Ler(File.ReadAllText(arquivo)).Nome; }
        catch (Exception e) when (e is PerfilInvalidoException or IOException or UnauthorizedAccessException) { return null; }
    }

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
        // Temporário e troca: uma queda no meio não deixa o perfil.json pela metade.
        string tmp = arquivo + ".tmp";
        File.WriteAllText(tmp, PerfilJson.Escrever(p));
        File.Move(tmp, arquivo, overwrite: true);
    }

    /// Nome da pasta de um perfil: caracteres inválidos em nome de arquivo viram "_".
    public static string NomeDePasta(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        return new string(nome.Trim().Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
    }

    private string ArquivoDe(string nome) => Path.Combine(Pasta, NomeDePasta(nome), "perfil.json");
}

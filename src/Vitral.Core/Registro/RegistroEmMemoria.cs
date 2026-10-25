namespace Vitral.Core.Registro;

/// Registro falso para testes. <see cref="Bloqueados"/> simula chaves protegidas (UCPD).
public sealed class RegistroEmMemoria : IRegistro
{
    private static readonly StringComparer Comparador = StringComparer.OrdinalIgnoreCase;
    private readonly Dictionary<(string, string), ValorRegistro> _valores = new(new ComparadorPar());

    public HashSet<(string Chave, string Nome)> Bloqueados { get; } = new(new ComparadorPar());

    public ValorRegistro Ler(string chave, string nome, TipoValor tipoEsperado) =>
        _valores.TryGetValue((chave, nome), out var v) ? v : new ValorRegistro(chave, nome, tipoEsperado, null);

    public void Gravar(ValorRegistro valor)
    {
        if (Bloqueados.Contains((valor.Chave, valor.Nome))) throw new EscritaBloqueadaException(valor);
        if (valor.Ausente) { Apagar(valor.Chave, valor.Nome); return; }
        _valores[(valor.Chave, valor.Nome)] = valor;
    }

    public void Apagar(string chave, string nome)
    {
        // Como o registro real: apagar valor protegido também é negado.
        if (Bloqueados.Contains((chave, nome)))
            throw new EscritaBloqueadaException(new ValorRegistro(chave, nome, TipoValor.Dword, null));
        _valores.Remove((chave, nome));
    }

    /// Subchaves que têm algum valor (no registro em memória, uma chave só existe através dos seus valores).
    public IReadOnlyList<string> SubChaves(string chave)
    {
        string prefixo = chave.TrimEnd('\\') + "\\";
        return _valores.Keys
            .Select(k => k.Item1)
            .Where(c => c.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            .Select(c => c[prefixo.Length..].Split('\\')[0])
            .Distinct(Comparador)
            .ToList();
    }

    private sealed class ComparadorPar : IEqualityComparer<(string, string)>
    {
        public bool Equals((string, string) a, (string, string) b) =>
            Comparador.Equals(a.Item1, b.Item1) && Comparador.Equals(a.Item2, b.Item2);

        public int GetHashCode((string, string) p) =>
            HashCode.Combine(Comparador.GetHashCode(p.Item1), Comparador.GetHashCode(p.Item2));
    }
}

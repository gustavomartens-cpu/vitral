namespace Vitral.Core.Doca;

/// Apps da loja (MSIX) moram em "WindowsApps\Nome_Versão_Arq_Recurso_Editor\…", e a pasta muda a cada atualização: a
/// antiga é apagada. Um fixado guardado por esse caminho perde o ícone e deixa de casar com a janela da versão nova.
public static class PacoteDaLoja
{
    private const string Raiz = @"\WindowsApps\";

    /// A família do pacote (Nome_Editor, igual em todas as versões), a pasta do pacote e o caminho dentro dela.
    public static (string Familia, string Pasta, string Resto)? Ler(string caminho)
    {
        int i = caminho.IndexOf(Raiz, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        int inicio = i + Raiz.Length;
        int fim = caminho.IndexOf('\\', inicio);
        if (fim < 0) return null;
        var partes = caminho[inicio..fim].Split('_');
        if (partes.Length != 5) return null;
        return ($"{partes[0]}_{partes[4]}", caminho[..fim], caminho[(fim + 1)..]);
    }

    /// O caminho sem a versão, para comparar duas versões do mesmo app.
    public static string SemVersao(string caminho) =>
        Ler(caminho) is { } p ? $@"{p.Familia}\{p.Resto}".ToLowerInvariant() : caminho.ToLowerInvariant();

    /// Fixados de versão apagada passam para a pasta atual do pacote (<paramref name="pastaAtual"/> pela família); o
    /// repetido que a troca deixar (o app fixado de novo depois de atualizar) sai, ficando o primeiro.
    public static IReadOnlyList<Fixado> Atualizar(IReadOnlyList<Fixado> lista, Func<string, bool> existe, Func<string, string?> pastaAtual)
    {
        var saida = new List<Fixado>();
        foreach (var f in lista)
        {
            var g = f;
            if (Ler(f.Caminho) is { } p && !existe(f.Caminho) && pastaAtual(p.Familia) is { } pasta)
                g = f with { Caminho = Path.Combine(pasta, p.Resto), Alvo = f.Alvo is { } a && Ler(a) is { } pa ? Path.Combine(pasta, pa.Resto) : f.Alvo };
            if (!saida.Any(s => string.Equals(s.Caminho, g.Caminho, StringComparison.OrdinalIgnoreCase))) saida.Add(g);
        }
        return saida;
    }
}

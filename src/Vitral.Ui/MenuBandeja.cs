namespace Vitral.Ui;

/// Um item do menu da bandeja. Ações: "perfil:&lt;nome&gt;", "desfazer", "original", "abrir", "sair".
public sealed record ItemMenu(string Texto, string Acao, bool Marcado, bool Separador, string? Icone = null);

/// Monta o menu da bandeja (spec §5): perfis (✓ no ativo), desfazer, original, abrir e sair.
public static class MenuBandeja
{
    private static readonly ItemMenu Separador = new("", "", false, true);

    public static IReadOnlyList<ItemMenu> Montar(IReadOnlyList<string> perfis, string? ativo)
    {
        var itens = perfis
            .Select(p => new ItemMenu(p, $"perfil:{p}", string.Equals(p, ativo, StringComparison.OrdinalIgnoreCase), false))
            .ToList();
        if (itens.Count > 0) itens.Add(Separador);
        itens.Add(new ItemMenu("Desfazer última mudança", "desfazer", false, false));
        itens.Add(new ItemMenu("Voltar ao Windows original", "original", false, false));
        itens.Add(Separador);
        itens.Add(new ItemMenu("Abrir Vitral", "abrir", false, false, ""));
        itens.Add(new ItemMenu("Sair", "sair", false, false, ""));
        return itens;
    }
}

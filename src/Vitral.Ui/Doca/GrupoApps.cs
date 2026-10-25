using Vitral.Core.Doca;
namespace Vitral.Ui.Doca;

/// Um botão da doca: um app (fixado, aberto ou os dois) e as suas janelas.
public sealed record GrupoApp(string Chave, string Nome, Fixado? Fixado, IReadOnlyList<JanelaApp> Janelas);

public enum TipoAcao { Abrir, Ativar, Minimizar, Escolher }

public sealed record AcaoClique(TipoAcao Tipo, IntPtr Janela);

/// Regras da doca (spec da doca §5): agrupar janelas por app e decidir o que o clique faz.
public static class GrupoApps
{
    private static string ChaveDe(JanelaApp j) => j.AppId ?? j.Executavel.ToLowerInvariant();

    private static bool Casa(Fixado f, JanelaApp j) =>
        (f.AppId is not null && string.Equals(f.AppId, j.AppId, StringComparison.OrdinalIgnoreCase))
        || (f.Alvo is not null && string.Equals(f.Alvo, j.Executavel, StringComparison.OrdinalIgnoreCase));

    /// Fixados primeiro, na ordem da lista; depois os abertos não fixados, na ordem em que aparecem.
    public static IReadOnlyList<GrupoApp> Montar(IReadOnlyList<Fixado> fixados, IReadOnlyList<JanelaApp> janelas)
    {
        var grupos = new List<GrupoApp>();
        var usadas = new HashSet<IntPtr>();
        foreach (var f in fixados)
        {
            var dele = janelas.Where(j => Casa(f, j)).ToList();
            foreach (var j in dele) usadas.Add(j.Handle);
            grupos.Add(new GrupoApp(f.AppId ?? (f.Alvo ?? f.Caminho).ToLowerInvariant(), f.Nome, f, dele));
        }
        foreach (var g in janelas.Where(j => !usadas.Contains(j.Handle)).GroupBy(ChaveDe))
            grupos.Add(new GrupoApp(g.Key, g.First().Titulo, null, g.ToList()));
        return grupos;
    }

    /// Sem janelas abre; com uma, minimiza se ativa ou traz para a frente; com várias, pede para escolher.
    public static AcaoClique Decidir(GrupoApp g, IntPtr ativa) => g.Janelas.Count switch
    {
        0 => new AcaoClique(TipoAcao.Abrir, IntPtr.Zero),
        1 when g.Janelas[0].Handle == ativa => new AcaoClique(TipoAcao.Minimizar, ativa),
        1 => new AcaoClique(TipoAcao.Ativar, g.Janelas[0].Handle),
        _ => new AcaoClique(TipoAcao.Escolher, IntPtr.Zero),
    };
}

/// Um item do menu do botão direito da doca.
public sealed record ItemMenu(string Texto, Action Acao);

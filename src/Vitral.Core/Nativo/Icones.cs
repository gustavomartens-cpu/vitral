using Vitral.Core.Barra;
using Vitral.Core.Registro;

namespace Vitral.Core.Nativo;

/// Um programa que já apareceu perto do relógio: executável (como o perfil guarda) e o nome para mostrar.
public sealed record ProgramaBandeja(string Executavel, string Nome);

/// Ícones perto do relógio (spec da barra de tarefas §4 e §5): HKCU\Control Panel\NotifyIconSettings\<id>,
/// com ExecutablePath, InitialTooltip e IsPromoted (1 à mostra, 0 na setinha). Valem na hora.
public static class Icones
{
    public const string Chave = @"Control Panel\NotifyIconSettings";
    private const string Promovido = "IsPromoted";

    private static string SubChave(string id) => $@"{Chave}\{id}";

    private static string? Executavel(IRegistro r, string id) =>
        r.Ler(SubChave(id), "ExecutablePath", TipoValor.Texto).Dado is string caminho && caminho.Length > 0
            ? Path.GetFileName(caminho)
            : null;

    private static bool Visivel(string executavel, IReadOnlyList<string> visiveis) =>
        visiveis.Contains(executavel, StringComparer.OrdinalIgnoreCase);

    /// Um por executável, pelo nome da dica (ou do executável sem ".exe"), em ordem alfabética.
    public static IReadOnlyList<ProgramaBandeja> Programas(IRegistro r) =>
        r.SubChaves(Chave)
            .Select(id => (Exe: Executavel(r, id), Dica: r.Ler(SubChave(id), "InitialTooltip", TipoValor.Texto).Dado as string))
            .Where(x => x.Exe is not null)
            .GroupBy(x => x.Exe!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProgramaBandeja(g.Key,
                g.Select(x => x.Dica).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? Path.GetFileNameWithoutExtension(g.Key)))
            .OrderBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// IsPromoted de cada ícone conforme a lista (os de fora vão para a setinha).
    public static IReadOnlyList<ValorRegistro> Calcular(IRegistro r, IReadOnlyList<string> visiveis) =>
        r.SubChaves(Chave)
            .Select(id => (Id: id, Exe: Executavel(r, id)))
            .Where(x => x.Exe is not null)
            .Select(x => new ValorRegistro(SubChave(x.Id), Promovido, TipoValor.Dword, Visivel(x.Exe!, visiveis) ? 1u : 0u))
            .ToList();

    /// Estado atual de todos os ícones, ausente incluído, para o retrato.
    public static IReadOnlyList<ValorRegistro> Retratar(IRegistro r) =>
        r.SubChaves(Chave).Select(id => r.Ler(SubChave(id), Promovido, TipoValor.Dword)).ToList();

    /// Para um ícone que acabou de aparecer: só se ele ainda não tem IsPromoted.
    public static ValorRegistro? ParaNovo(IRegistro r, string id, IReadOnlyList<string> visiveis)
    {
        if (!r.Ler(SubChave(id), Promovido, TipoValor.Dword).Ausente) return null;
        return Executavel(r, id) is { } exe
            ? new ValorRegistro(SubChave(id), Promovido, TipoValor.Dword, Visivel(exe, visiveis) ? 1u : 0u)
            : null;
    }

    /// O que o observador faz a cada mudança: aplica a lista guardada aos ícones novos. Devolve quantos gravou.
    /// Com "não mexer" aplicado (lista nula), não faz nada.
    public static int AplicarNovos(IRegistro r, EstadoIcones estado)
    {
        if (estado.Atual() is not { } visiveis) return 0;
        int gravados = 0;
        foreach (var id in r.SubChaves(Chave))
        {
            if (ParaNovo(r, id, visiveis) is not { } valor) continue;
            r.Gravar(valor);
            gravados++;
        }
        return gravados;
    }

    public static bool EhDeIcone(ValorRegistro v) => v.Chave.StartsWith(Chave + @"\", StringComparison.OrdinalIgnoreCase);
}

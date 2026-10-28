using System.Text.RegularExpressions;

namespace Vitral.Core.Agenda;

/// As regras da agenda (spec da agenda §4), sem rede nem tela.
public static partial class RegrasAgenda
{
    private static readonly string[] Paleta = ["#F2B544", "#3DDBB5", "#FF8A8A", "#B28DFF", "#9AD45B", "#F08AC2"];

    /// Uma lista só, sem cancelados nem recusados: dia inteiro primeiro, depois por horário e título.
    public static IReadOnlyList<Compromisso> Juntar(IEnumerable<Compromisso> todos) =>
        todos.Where(c => !c.Cancelado && !c.Recusado)
            .OrderBy(c => c.DiaInteiro ? 0 : 1).ThenBy(c => c.Inicio).ThenBy(c => c.Titulo, StringComparer.CurrentCulture)
            .ToList();

    [GeneratedRegex(@"https://meet\.google\.com/[a-z]{3}-[a-z]{4}-[a-z]{3}")] private static partial Regex Meet();
    [GeneratedRegex(@"https://teams\.microsoft\.com/l/meetup-join/[^\s""<>]+")] private static partial Regex Teams();
    [GeneratedRegex(@"https://[\w.-]*zoom\.us/j/[^\s""<>]+")] private static partial Regex Zoom();

    /// Link de reunião achado no texto (quando o provedor não dá o campo próprio): Meet, Teams, Zoom, nessa ordem.
    public static Uri? ExtrairLink(string? local, string? descricao)
    {
        string texto = $"{local} {descricao}";
        foreach (var padrao in new[] { Meet(), Teams(), Zoom() })
            if (padrao.Match(texto) is { Success: true } m && Uri.TryCreate(m.Value, UriKind.Absolute, out var uri)) return uri;
        return null;
    }

    /// Os compromissos a avisar agora (spec §4.3): na antecedência, ainda não terminados e não avisados com esse horário.
    /// Os devolvidos entram em <paramref name="jaAvisados"/>.
    public static IReadOnlyList<Compromisso> ProximosAvisos(IEnumerable<Compromisso> lista, DateTimeOffset agora,
        TimeSpan antecedencia, ISet<(string Id, DateTimeOffset Inicio)> jaAvisados)
    {
        var avisos = lista.Where(c => !c.DiaInteiro && c.Inicio - antecedencia <= agora && agora < c.Fim
            && !jaAvisados.Contains((c.Id, c.Inicio))).ToList();
        foreach (var c in avisos) jaAvisados.Add((c.Id, c.Inicio));
        return avisos;
    }

    /// Os dias do mês com compromisso, no fuso dado (um compromisso de vários dias marca todos).
    public static ISet<int> DiasComCompromisso(IEnumerable<Compromisso> lista, int ano, int mes, TimeZoneInfo fuso)
    {
        var dias = new HashSet<int>();
        foreach (var c in lista)
        {
            var primeiro = Data(c.Inicio, fuso);
            var ultimo = Data(c.Fim > c.Inicio ? c.Fim.AddTicks(-1) : c.Inicio, fuso);
            for (var d = primeiro; d <= ultimo; d = d.AddDays(1))
                if (d.Year == ano && d.Month == mes) dias.Add(d.Day);
        }
        return dias;
    }

    /// Os compromissos que tocam o dia (no fuso), na ordem da lista.
    public static IReadOnlyList<Compromisso> DoDia(IEnumerable<Compromisso> lista, DateOnly dia, TimeZoneInfo fuso)
    {
        var (inicio, fim) = DiaInteiro(dia, dia.AddDays(1), fuso);
        return lista.Where(c => c.Inicio < fim && c.Fim > inicio).ToList();
    }

    /// Dia inteiro do provedor (datas, fim exclusivo) em horário absoluto: meia-noite local até meia-noite local.
    public static (DateTimeOffset Inicio, DateTimeOffset Fim) DiaInteiro(DateOnly inicio, DateOnly fimExclusivo, TimeZoneInfo fuso) =>
        (MeiaNoite(inicio, fuso), MeiaNoite(fimExclusivo, fuso));

    /// 1ª conta Google azul, 1ª Microsoft azul-claro; as seguintes, da paleta, pela ordem em que foram ligadas.
    public static string CorDaConta(ProvedorAgenda provedor, int ordem) => ordem == 0
        ? (provedor == ProvedorAgenda.Google ? "#4C7DF0" : "#2BA9E0")
        : Paleta[(ordem - 1) % Paleta.Length];

    private static DateTimeOffset MeiaNoite(DateOnly dia, TimeZoneInfo fuso)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, fuso.GetUtcOffset(local));
    }

    private static DateOnly Data(DateTimeOffset t, TimeZoneInfo fuso) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t, fuso).DateTime);
}

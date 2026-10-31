namespace Vitral.Core.Agenda;

public enum ProvedorAgenda { Google, Microsoft }

/// Uma conta ligada (spec da agenda §3). Só identifica: o acesso fica no cofre de tokens do App, nunca aqui.
public sealed record ContaAgenda(ProvedorAgenda Provedor, string Email, string Cor);

/// Um compromisso já normalizado, venha do Google ou da Microsoft.
public sealed record Compromisso(string Id, ContaAgenda Conta, string Titulo, DateTimeOffset Inicio, DateTimeOffset Fim,
    bool DiaInteiro, string? Local, Uri? LinkReuniao, Uri? LinkEvento, bool Recusado, bool Cancelado);

/// Uma conta de agenda que sabe ler os compromissos de um intervalo.
public interface IFonteAgenda
{
    ContaAgenda Conta { get; }
    Task<IReadOnlyList<Compromisso>> Ler(DateTimeOffset de, DateTimeOffset ate, CancellationToken cancelar);
}

/// Uma agenda além da principal que a pessoa escolheu ler, na conta do e-mail.
public sealed record AgendaEscolhida(ProvedorAgenda Provedor, string Email, string Id);

/// A agenda no config.json: as contas ligadas (sem token), a antecedência do aviso e as agendas escolhidas além da
/// principal de cada conta (nulo = nenhuma).
public sealed record AgendaConfig(IReadOnlyList<ContaAgenda> Contas, int AntecedenciaMin = 10, bool Avisar = true,
    IReadOnlyList<AgendaEscolhida>? Escolhidas = null)
{
    private static bool DaConta(AgendaEscolhida a, ContaAgenda c) =>
        a.Provedor == c.Provedor && string.Equals(a.Email, c.Email, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> AgendasDe(ContaAgenda conta) =>
        (Escolhidas ?? []).Where(a => DaConta(a, conta)).Select(a => a.Id).ToList();

    /// Troca as escolhidas da conta pelas <paramref name="ids"/>; as das outras contas ficam.
    public AgendaConfig ComAgendas(ContaAgenda conta, IEnumerable<string> ids) => this with
    {
        Escolhidas = [.. (Escolhidas ?? []).Where(a => !DaConta(a, conta)), .. ids.Select(id => new AgendaEscolhida(conta.Provedor, conta.Email, id))],
    };
}

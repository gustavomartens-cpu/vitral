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

/// A agenda no config.json: as contas ligadas (sem token) e a antecedência do aviso.
public sealed record AgendaConfig(IReadOnlyList<ContaAgenda> Contas, int AntecedenciaMin = 10, bool Avisar = true);

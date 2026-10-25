namespace Vitral.Core.Perfis;

public enum PesquisaBarra { Oculta, Icone, Caixa }

public enum AgruparBotoes { Sempre, QuandoCheia, Nunca }

/// Seção "barraTarefas" do perfil (esquema 3): a barra de tarefas do Windows. Campo nulo = não mexer.
/// <see cref="IconesVisiveis"/>: programas à mostra perto do relógio (pelo nome do executável); lista vazia esconde todos.
public sealed record BarraTarefasPerfil(
    PesquisaBarra? Pesquisa = null,
    bool? VisaoTarefas = null,
    bool? MostrarAreaDeTrabalho = null,
    AgruparBotoes? Agrupar = null,
    bool? OcultarAutomaticamente = null,
    bool? TodosOsMonitores = null,
    bool? Selos = null,
    bool? Piscar = null,
    IReadOnlyList<string>? IconesVisiveis = null)
{
    /// A lista é comparada pelo conteúdo, sem diferenciar maiúsculas (nomes de executável no Windows).
    public bool Equals(BarraTarefasPerfil? outro) =>
        outro is not null
        && Pesquisa == outro.Pesquisa && VisaoTarefas == outro.VisaoTarefas
        && MostrarAreaDeTrabalho == outro.MostrarAreaDeTrabalho && Agrupar == outro.Agrupar
        && OcultarAutomaticamente == outro.OcultarAutomaticamente && TodosOsMonitores == outro.TodosOsMonitores
        && Selos == outro.Selos && Piscar == outro.Piscar
        && (IconesVisiveis is null
            ? outro.IconesVisiveis is null
            : outro.IconesVisiveis is not null && IconesVisiveis.SequenceEqual(outro.IconesVisiveis, StringComparer.OrdinalIgnoreCase));

    public override int GetHashCode() =>
        HashCode.Combine(Pesquisa, VisaoTarefas, MostrarAreaDeTrabalho, Agrupar, OcultarAutomaticamente, TodosOsMonitores, Selos,
            HashCode.Combine(Piscar, IconesVisiveis?.Count));
}

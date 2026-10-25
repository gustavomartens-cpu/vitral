namespace Vitral.Core.Perfis;

public enum Modo { Claro, Escuro }

public enum Alinhamento { Esquerda, Centro }

/// Um perfil visual (spec §4.2). Seção ou campo nulo significa "não mexer".
public sealed record Perfil(int Esquema, string Nome, string? BuildTestada, AjustesNativosPerfil? Nativo, BarraTopoPerfil? BarraTopo = null,
    BarraTarefasPerfil? BarraTarefas = null, BarraBaixoPerfil? BarraBaixo = null);

/// Ajustes que o próprio Windows oferece. Cores em "#RRGGBB".
public sealed record AjustesNativosPerfil(
    Modo? ModoSistema,
    Modo? ModoApps,
    string? CorDestaque,
    bool? CorDestaqueAutomatica,
    bool? Transparencia,
    bool? DestaqueNaBarraEIniciar,
    bool? DestaqueNosTitulos,
    Alinhamento? AlinhamentoBarra);

public class PerfilInvalidoException(string motivo, Exception? causa = null) : Exception(motivo, causa);

/// Importação recusada porque já há perfis locais com estes nomes (use substituir para sobrescrever).
public sealed class PerfilJaExisteException(IReadOnlyList<string> nomes, string motivo) : PerfilInvalidoException(motivo)
{
    public IReadOnlyList<string> Nomes { get; } = nomes;
}

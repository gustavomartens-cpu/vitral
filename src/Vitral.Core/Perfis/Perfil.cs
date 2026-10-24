namespace Vitral.Core.Perfis;

public enum Modo { Claro, Escuro }

public enum Alinhamento { Esquerda, Centro }

/// Um perfil visual (spec §4.2). Seção ou campo nulo significa "não mexer".
public sealed record Perfil(int Esquema, string Nome, string? BuildTestada, AjustesNativosPerfil? Nativo);

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

public sealed class PerfilInvalidoException(string motivo, Exception? causa = null) : Exception(motivo, causa);

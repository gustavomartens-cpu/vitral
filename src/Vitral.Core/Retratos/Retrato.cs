using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Retratos;

/// Fotografia dos valores controlados antes de uma mudança. Valor ausente (Dado nulo) volta a ser ausente na reversão.
/// <see cref="BarraTopo"/> nulo (retratos de antes da barra) significa "não mexer" na barra ao reverter.
/// <see cref="IconesVisiveis"/> é a lista de ícones aplicada naquele momento (nula = não mexer), restaurada junto.
public sealed record Retrato(DateTimeOffset Quando, string? PerfilAplicado, IReadOnlyList<ValorRegistro> Valores,
    BarraTopoPerfil? BarraTopo = null, IReadOnlyList<string>? IconesVisiveis = null, BarraBaixoPerfil? BarraBaixo = null,
    AparenciaPerfil? Aparencia = null, FormasPerfil? Formas = null);

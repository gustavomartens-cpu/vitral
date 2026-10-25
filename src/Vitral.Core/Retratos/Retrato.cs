using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Retratos;

/// Fotografia dos valores controlados antes de uma mudança. Valor ausente (Dado nulo) volta a ser ausente na reversão.
/// <see cref="BarraTopo"/> nulo (retratos de antes da barra) significa "não mexer" na barra ao reverter.
public sealed record Retrato(DateTimeOffset Quando, string? PerfilAplicado, IReadOnlyList<ValorRegistro> Valores,
    BarraTopoPerfil? BarraTopo = null);

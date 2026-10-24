using Vitral.Core.Registro;

namespace Vitral.Core.Retratos;

/// Fotografia dos valores controlados antes de uma mudança. Valor ausente (Dado nulo) volta a ser ausente na reversão.
public sealed record Retrato(DateTimeOffset Quando, string? PerfilAplicado, IReadOnlyList<ValorRegistro> Valores);

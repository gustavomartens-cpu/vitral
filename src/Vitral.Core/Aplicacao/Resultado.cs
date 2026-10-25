using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Aplicacao;

/// Resultado de aplicar um perfil. Ok só quando nada ficou de fora (spec §6: nunca relatar sucesso parcial como total).
public sealed record Resultado(
    bool Ok,
    IReadOnlyList<ValorRegistro> Aplicados,
    IReadOnlyList<(ValorRegistro Valor, string Motivo)> NaoAplicados,
    Retrato RetratoAnterior,
    string? ArquivoRetratoAnterior = null);

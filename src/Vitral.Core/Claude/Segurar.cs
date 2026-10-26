namespace Vitral.Core.Claude;

/// Quando o gancho segura a sessão para a resposta pelo notch (spec da resposta §3.3): só no Stop, e só com a janela do
/// Claude fora da frente (com o app na frente, o Gustavo responde lá).
public static class Segurar
{
    public static bool NoStop(string entrada, bool claudeNaFrente) =>
        !claudeNaFrente && entrada.Contains("\"Stop\"", StringComparison.Ordinal) && AvisoClaude.DeGancho(entrada, _ => null)?.Tipo == TipoAviso.Terminou;
}

namespace Vitral.Core.Claude;

/// Quando o gancho segura a sessão para a resposta pelo notch (spec da resposta §3.3): só no Stop, só com a janela do
/// Claude fora da frente (com o app na frente, o Gustavo responde lá), só numa sessão com gente (não em `claude -p`,
/// tarefa agendada ou SDK) e só com o gancho já religado com o tempo da resposta (senão o Claude o cortaria no tempo
/// padrão, no meio da digitação). E só no fim de um turno que o Gustavo pediu (R4 da pesquisa de notches): o de uma
/// tarefa em segundo plano, de um subagente ou de outra sessão não vira ficha.
public static class Segurar
{
    public static bool NoStop(string entrada, bool claudeNaFrente, bool sessaoComGente, bool ganchoComTempo, bool turnoDoGustavo = true) =>
        !claudeNaFrente && sessaoComGente && ganchoComTempo && turnoDoGustavo
        && entrada.Contains("\"Stop\"", StringComparison.Ordinal) && AvisoClaude.DeGancho(entrada, _ => null)?.Tipo == TipoAviso.Terminou;

    /// Pelas variáveis que o Claude Code passa ao gancho: a sessão é do app do Claude e tem alguém atendendo.
    public static bool SessaoComGente(string? pontoDeEntrada, string? atendida) =>
        pontoDeEntrada == "claude-desktop" && atendida != "0";
}

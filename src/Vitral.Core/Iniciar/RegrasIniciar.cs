namespace Vitral.Core.Iniciar;

/// Por que o Iniciar está com o visual do Windows (spec do Iniciar §5). Nenhum = com o do Vitral.
public enum MotivoIniciar { Nenhum, Windhawk, Recusou, WindowsMudou, SemComponente }

/// As decisões do ServicoIniciar que não dependem do Windows.
public static class RegrasIniciar
{
    public static string Texto(MotivoIniciar motivo) => motivo switch
    {
        MotivoIniciar.Nenhum => "Iniciar com o visual do Vitral",
        MotivoIniciar.Windhawk => "Iniciar com o visual do Windows: o Windhawk está usando o Iniciar",
        MotivoIniciar.Recusou => "Iniciar com o visual do Windows: o Iniciar não aceitou o componente",
        MotivoIniciar.WindowsMudou => "Iniciar com o visual do Windows: o Windows mudou o Iniciar",
        _ => "Iniciar com o visual do Windows: o componente não foi encontrado",
    };

    /// Conecta no modo Vitral, a um processo do Iniciar ainda não conectado (o Windows o reinicia às vezes), e nunca
    /// com o Windhawk aberto: só um consumidor de XAML Diagnostics por processo.
    public static bool DeveConectar(ModoIniciar modo, int? pidIniciar, int? pidConectado, bool windhawkAberto) =>
        modo == ModoIniciar.Vitral && pidIniciar is { } pid && pid != pidConectado && !windhawkAberto;

    /// As pastas de versões anteriores da DLL: o Iniciar pode estar segurando uma delas, então quem apaga tolera falha.
    public static IReadOnlyList<string> PastasAntigas(IEnumerable<string> pastas, string versaoAtual) =>
        pastas.Where(p => !string.Equals(Path.GetFileName(p.TrimEnd('\\', '/')), versaoAtual, StringComparison.OrdinalIgnoreCase)).ToList();
}

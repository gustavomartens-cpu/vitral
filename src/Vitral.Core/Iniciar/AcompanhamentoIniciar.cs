namespace Vitral.Core.Iniciar;

/// O estado da conexão com o processo do Iniciar, conferido a cada 5 s pelo ServicoIniciar (revisão final da 0.14.0):
/// guarda o resultado por processo (voltar a "Combinar" mostra de novo o que vale), tenta de novo depois de uma recusa
/// com espera crescente e lê só o trecho novo do log da DLL (ADVISE atrasado vira conectado; SEM_FUNDO vale até um
/// PINTADO depois dele).
public sealed class AcompanhamentoIniciar
{
    private static readonly TimeSpan[] Esperas = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    private int? _pid;
    private MotivoIniciar? _resultado;
    private DateTime _proximaTentativa = DateTime.MaxValue;
    private int _recusas;

    /// <param name="logNovo">O que a DLL escreveu no log desde a última conferência.</param>
    /// <returns>Se deve conectar agora, e o estado a mostrar (nulo = sem linha: modo Windows ou ainda sem resultado).</returns>
    public (bool Conectar, MotivoIniciar? Estado) Conferir(ModoIniciar modo, int? pid, bool windhawk, string logNovo, DateTime agora)
    {
        if (modo != ModoIniciar.Vitral || pid is not { } atual) return (false, null);
        if (atual != _pid)
        {
            if (windhawk) return (false, MotivoIniciar.Windhawk); // só um consumidor por processo
            (_pid, _resultado, _recusas) = (atual, null, 0);
            _proximaTentativa = DateTime.MaxValue; // tentando: até o resultado chegar
            return (true, null);
        }
        LerLog(logNovo);
        if (_resultado == MotivoIniciar.Recusou && agora >= _proximaTentativa)
        {
            _proximaTentativa = DateTime.MaxValue;
            return (true, _resultado);
        }
        return (false, _resultado);
    }

    /// A tentativa de conexão terminou; o resultado de um processo que não é mais o atual não conta.
    public void Resultado(int pid, bool entrou, DateTime agora)
    {
        if (pid != _pid) return;
        if (entrou) { (_resultado, _recusas) = (MotivoIniciar.Nenhum, 0); return; }
        _resultado = MotivoIniciar.Recusou;
        _proximaTentativa = agora + Esperas[Math.Min(_recusas, Esperas.Length - 1)];
        _recusas++;
    }

    private void LerLog(string log)
    {
        if (log.Length == 0) return;
        if (_resultado == MotivoIniciar.Recusou && log.Contains("ADVISE|hr=0")) (_resultado, _recusas) = (MotivoIniciar.Nenhum, 0);
        if (_resultado is not (MotivoIniciar.Nenhum or MotivoIniciar.WindowsMudou)) return;
        int semFundo = log.LastIndexOf("SEM_FUNDO", StringComparison.Ordinal);
        int pintado = log.LastIndexOf("PINTADO|", StringComparison.Ordinal);
        if (semFundo > pintado) _resultado = MotivoIniciar.WindowsMudou;
        else if (pintado >= 0) _resultado = MotivoIniciar.Nenhum;
    }
}

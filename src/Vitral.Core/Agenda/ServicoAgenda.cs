namespace Vitral.Core.Agenda;

/// A fonte não conseguiu renovar o acesso: a pessoa precisa entrar de novo na conta.
public sealed class ContaPrecisaEntrarException(string? mensagem = null) : Exception(mensagem ?? "A conta precisa entrar de novo.");

public enum EstadoConta { Ok, PrecisaEntrar, SemConexao }

/// Junta as agendas das contas ligadas (spec da agenda §4.1): guarda a última leitura boa de cada conta, para uma conta
/// fora do ar (ou que precisa entrar de novo) não esvaziar a lista, e decide os avisos sem repetir.
public sealed class ServicoAgenda(Func<DateTimeOffset> agora, TimeZoneInfo fuso)
{
    private readonly object _trava = new();
    private readonly SemaphoreSlim _umaDeCadaVez = new(1, 1);
    private readonly HashSet<(string Id, DateTimeOffset Inicio)> _avisados = [];
    private Dictionary<ContaAgenda, (IReadOnlyList<Compromisso> Lista, EstadoConta Estado, DateTimeOffset? AtualizadoEm)> _porConta = [];
    private IReadOnlyList<IFonteAgenda> _fontes = [];

    public IReadOnlyList<Compromisso> Compromissos { get; private set; } = [];

    public IReadOnlyDictionary<ContaAgenda, (EstadoConta Estado, DateTimeOffset? AtualizadoEm)> Contas { get; private set; } =
        new Dictionary<ContaAgenda, (EstadoConta, DateTimeOffset?)>();

    public TimeZoneInfo Fuso => fuso;

    /// A lista ou o estado de uma conta mudou.
    public event Action? Mudou;

    /// As contas ligadas agora: as que saíram somem da lista na hora.
    public void DefinirFontes(IReadOnlyList<IFonteAgenda> fontes)
    {
        lock (_trava)
        {
            _fontes = fontes;
            _porConta = _porConta.Where(p => fontes.Any(f => f.Conta == p.Key)).ToDictionary(p => p.Key, p => p.Value);
        }
        Publicar();
    }

    /// Lê todas as contas (em paralelo) no intervalo de três meses em volta do atual. Uma leitura de cada vez: duas juntas
    /// renovariam o mesmo acesso ao mesmo tempo, e uma mais velha poderia sobrescrever a mais nova.
    public async Task Atualizar(CancellationToken cancelar)
    {
        await _umaDeCadaVez.WaitAsync(cancelar);
        try { await Ler(cancelar); }
        finally { _umaDeCadaVez.Release(); }
    }

    private async Task Ler(CancellationToken cancelar)
    {
        IReadOnlyList<IFonteAgenda> fontes;
        lock (_trava) fontes = _fontes;
        var (de, ate) = Intervalo();
        var leituras = await Task.WhenAll(fontes.Select(async f =>
        {
            try { return (f.Conta, Lista: await f.Ler(de, ate, cancelar), Estado: EstadoConta.Ok); }
            catch (ContaPrecisaEntrarException) { return (f.Conta, Lista: (IReadOnlyList<Compromisso>?)null, Estado: EstadoConta.PrecisaEntrar); }
            catch (OperationCanceledException) when (cancelar.IsCancellationRequested) { throw; }
            // Qualquer outro erro (rede, resposta estranha, cofre ocupado) fica só nesta conta: as outras seguem.
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return (f.Conta, Lista: null, Estado: EstadoConta.SemConexao);
            }
        }));
        lock (_trava)
        {
            foreach (var (conta, lista, estado) in leituras)
            {
                if (!_fontes.Any(f => f.Conta == conta)) continue; // removida durante a leitura
                var anterior = _porConta.GetValueOrDefault(conta);
                _porConta[conta] = lista is null
                    ? (anterior.Lista ?? [], estado, anterior.AtualizadoEm)
                    : (lista, estado, agora());
            }
        }
        Publicar();
    }

    /// Os compromissos a avisar agora; cada um só uma vez por horário.
    public IReadOnlyList<Compromisso> AvisosParaMostrar(TimeSpan antecedencia)
    {
        lock (_trava) return RegrasAgenda.ProximosAvisos(Compromissos, agora(), antecedencia, _avisados);
    }

    /// Os compromissos já avisados que saíram da agenda ou mudaram de horário (o lembrete deles sai); cada um uma vez.
    public IReadOnlyList<string> AvisadosQueSumiram()
    {
        lock (_trava)
        {
            var agora = Compromissos.Select(c => (c.Id, c.Inicio)).ToHashSet();
            var sumiram = _avisados.Where(a => !agora.Contains(a)).ToList();
            foreach (var a in sumiram) _avisados.Remove(a);
            return sumiram.Select(a => a.Id).Distinct().ToList();
        }
    }

    private (DateTimeOffset De, DateTimeOffset Ate) Intervalo()
    {
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(agora(), fuso).DateTime);
        var primeiro = new DateOnly(hoje.Year, hoje.Month, 1);
        return RegrasAgenda.DiaInteiro(primeiro.AddMonths(-1), primeiro.AddMonths(2), fuso);
    }

    private void Publicar()
    {
        bool mudou;
        lock (_trava)
        {
            var lista = RegrasAgenda.Juntar(_porConta.Values.SelectMany(v => v.Lista));
            var contas = _porConta.ToDictionary(p => p.Key, p => (p.Value.Estado, p.Value.AtualizadoEm));
            mudou = !lista.SequenceEqual(Compromissos) || contas.Count != Contas.Count
                || contas.Any(p => !Contas.TryGetValue(p.Key, out var v) || v != p.Value);
            Compromissos = lista;
            Contas = contas;
        }
        if (mudou) Mudou?.Invoke();
    }
}

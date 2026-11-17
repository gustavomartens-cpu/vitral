using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

/// Quantas sessões do Claude estão rodando e quantas esperam o Gustavo, e se uma acabou de terminar (o check).
public readonly record struct ResumoAtividade(int Rodando, int Esperando, bool Terminou);

/// O que se leu da transcrição de uma sessão: quando foi escrita pela última vez e se o fim é um Ctrl+C.
public readonly record struct LeituraTranscricao(DateTime UltimaEscrita, bool Interrompida);

/// O Claude trabalhando, para o notch fechado (spec do notch fechado §3.1, R1). Uma sessão entra com o começo do turno
/// (gancho UserPromptSubmit) ou com um pedido aberto no notch, espera com um pedido ou um Notification de pedido, e sai
/// com o Stop, com o Ctrl+C (marca na transcrição) ou depois de <see cref="MinutosSemMudar"/> sem a transcrição mudar.
/// O Stop de um turno que o Gustavo pediu acende o check por <see cref="SegundosCheck"/>.
public sealed class AtividadeClaude(Func<DateTime> agora)
{
    /// FolgaRetomou: a transcrição escrita logo depois do aviso de espera ainda é do pedido; só uma escrita mais de 2 s
    /// depois diz que o Claude voltou a trabalhar.
    public const double SegundosCheck = 3, MinutosSemMudar = 30, FolgaRetomou = 2;

    /// O Notification que o Claude Code manda quando fica ocioso esperando a próxima mensagem: não é pedido.
    public const string Ocioso = "idle_prompt";

    private sealed class Sessao(DateTime inicio)
    {
        public string? Transcricao;
        public DateTime UltimaAtividade = inicio;
        public DateTime? EsperaDesde;
        public int Pedidos;
        public bool Esperando => Pedidos > 0 || EsperaDesde is not null;
    }

    private readonly Dictionary<string, Sessao> _sessoes = [];
    private DateTime? _checkAte;

    public event Action? Mudou;

    public ResumoAtividade Resumo
    {
        get
        {
            int esperando = _sessoes.Values.Count(s => s.Esperando);
            return new(_sessoes.Count - esperando, esperando, _checkAte is not null);
        }
    }

    /// Há o que conferir de tempos em tempos: sessões abertas ou o check aceso.
    public bool PrecisaConferir => _sessoes.Count > 0 || _checkAte is not null;

    public void Chegou(AvisoClaude aviso) => Mudando(() =>
    {
        var chave = aviso.ChaveSessao;
        switch (aviso.Tipo)
        {
            case TipoAviso.Comecou:
                var s = Pegar(chave);
                s.Transcricao = aviso.Transcricao ?? s.Transcricao;
                s.UltimaAtividade = agora();
                s.EsperaDesde = null; // mensagem nova: o Claude voltou a trabalhar
                break;
            case TipoAviso.Esperando when aviso.TipoNotificacao != Ocioso && _sessoes.TryGetValue(chave, out var esperando):
                esperando.EsperaDesde = agora();
                break;
            case TipoAviso.Terminou when _sessoes.Remove(chave):
                if (!aviso.DeOutraOrigem) _checkAte = agora().AddSeconds(SegundosCheck);
                break;
        }
    });

    /// Um pedido de permissão ou uma pergunta da sessão entrou no notch.
    public void PedidoAbriu(string chave) => Mudando(() => Pegar(chave).Pedidos++);

    /// O pedido saiu (respondido, resolvido no app ou sem resposta): sem outro aberto, a sessão volta a rodar.
    public void PedidoSaiu(string chave) => Mudando(() =>
    {
        if (!_sessoes.TryGetValue(chave, out var s)) return;
        s.Pedidos = Math.Max(0, s.Pedidos - 1);
        if (s.Pedidos == 0) s.EsperaDesde = null;
    });

    /// Confere as transcrições (<paramref name="ler"/> devolve null quando não deu para ler) e apaga o check vencido.
    public void Conferir(Func<string, LeituraTranscricao?> ler) => Mudando(() =>
    {
        var hoje = agora();
        if (_checkAte <= hoje) _checkAte = null;
        foreach (var (chave, s) in _sessoes.ToList())
        {
            var leitura = s.Transcricao is { } caminho ? ler(caminho) : null;
            if (leitura?.Interrompida == true) { _sessoes.Remove(chave); continue; }
            if (leitura is { } l && l.UltimaEscrita > s.UltimaAtividade) s.UltimaAtividade = l.UltimaEscrita;
            if (hoje - s.UltimaAtividade > TimeSpan.FromMinutes(MinutosSemMudar)) { _sessoes.Remove(chave); continue; }
            if (s is { Pedidos: 0, EsperaDesde: { } desde } && leitura is { } lida && lida.UltimaEscrita > desde.AddSeconds(FolgaRetomou))
                s.EsperaDesde = null;
        }
    });

    private Sessao Pegar(string chave)
    {
        if (!_sessoes.TryGetValue(chave, out var s)) _sessoes[chave] = s = new Sessao(agora());
        return s;
    }

    private void Mudando(Action acao)
    {
        var antes = Resumo;
        acao();
        if (Resumo != antes) Mudou?.Invoke();
    }
}

using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

/// A ficha de pergunta (spec das perguntas §3 e §4): a pergunta do Claude é um pedido da mesma fila das permissões, com
/// as opções em botões. Escolha única responde no clique; múltipla marca e envia; várias perguntas vão uma de cada vez e
/// a resposta sai na última.
public sealed partial class EstadoNotch
{
    public const double TempoPergunta = 300;

    private readonly Dictionary<string, string> _respostasDaPergunta = [];
    private readonly HashSet<string> _marcadas = new(StringComparer.Ordinal);

    /// Qual pergunta do pedido está na ficha (0 = a primeira).
    public int PerguntaAtual { get; private set; }

    /// As opções marcadas na pergunta múltipla da ficha.
    public IReadOnlySet<string> Marcadas => _marcadas;

    /// A pergunta múltipla tem ao menos uma marcada ("Enviar" ligado).
    public bool PodeEnviar => _marcadas.Count > 0;

    /// O tempo de um pedido na fila: a pergunta do Claude pede leitura (5 min); a permissão, 60 s.
    public static double Tempo(PedidoPermissao pedido) => EhPergunta(pedido) ? TempoPergunta : TempoPedido;

    private static bool EhPergunta(PedidoPermissao pedido) => pedido.Ferramenta == RegrasPerguntas.Ferramenta;

    /// O tempo do pedido que está na ficha (a barra de tempo).
    public double TempoDoAtual => PedidoAtual is { } p ? Tempo(p) : TempoPedido;

    /// Um clique numa opção de uma pergunta de escolha única: guarda e vai para a próxima, ou responde na última.
    public bool Escolheu(string id, string rotulo)
    {
        if (!NaFicha(id) || Atual() is not { Multipla: false } pergunta || pergunta.Opcoes.All(o => o.Rotulo != rotulo)) return false;
        _respostasDaPergunta[pergunta.Texto] = rotulo;
        return Seguir(id);
    }

    /// Marca ou desmarca uma opção da pergunta múltipla da ficha.
    public void Marcar(string id, string rotulo)
    {
        if (!NaFicha(id) || Atual() is not { Multipla: true } pergunta || pergunta.Opcoes.All(o => o.Rotulo != rotulo)) return;
        if (!_marcadas.Remove(rotulo)) _marcadas.Add(rotulo);
        Mudou?.Invoke();
    }

    /// "Enviar" da pergunta múltipla: guarda as marcadas e vai para a próxima, ou responde na última.
    public bool Enviar(string id)
    {
        if (!NaFicha(id) || Atual() is not { Multipla: true } pergunta || _marcadas.Count == 0) return false;
        _respostasDaPergunta[pergunta.Texto] = RegrasPerguntas.Juntar(pergunta, _marcadas);
        return Seguir(id);
    }

    /// O pedido <paramref name="id"/> é o da ficha e já passou a espera do clique (um clique dado na ficha anterior, ou
    /// na pergunta anterior, não cai nesta).
    private bool NaFicha(string id) => PedidoAtual is { } p && p.Id == id && (agora() - _mostrou).TotalSeconds >= EsperaClique;

    private Pergunta? Atual() =>
        PedidoAtual?.Perguntas is { } perguntas && RegrasPerguntas.Cabe(perguntas) && PerguntaAtual < perguntas.Count ? perguntas[PerguntaAtual] : null;

    private bool Seguir(string id)
    {
        var perguntas = PedidoAtual!.Perguntas!;
        _marcadas.Clear();
        if (PerguntaAtual + 1 < perguntas.Count)
        {
            PerguntaAtual++;
            _mostrou = agora();
            Mudou?.Invoke();
            return true;
        }
        return Responder(id, new RespostaPedido(EscolhaPermissao.Permitir, new Dictionary<string, string>(_respostasDaPergunta))) is not null;
    }

    private void ZerarPergunta()
    {
        PerguntaAtual = 0;
        _respostasDaPergunta.Clear();
        _marcadas.Clear();
    }
}

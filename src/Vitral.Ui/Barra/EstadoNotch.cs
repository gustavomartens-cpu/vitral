using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

public enum ModoNotch { Normal, Aviso, Lembrete, Permissao }

public enum AcaoNotch { Nenhuma, AbrirClaude, AbrirCalendario, AbrirNotificacoes, Reiniciar }

/// Onde o clique caiu: no meio (relógio ou aviso) ou numa das laterais (spec do notch vivo §3.4).
public enum ParteNotch { Meio, Esquerda, Direita }

/// O notch vivo (spec do notch vivo §3.3 e §3.4; spec das permissões §4.2):
/// - o aviso fica 6 s (segurado com o mouse em cima) e recolhe para o lembrete;
/// - fora do aviso, o mouse parado 250 ms abre as laterais (Claude à esquerda, notificações à direita);
/// - pedidos de permissão têm prioridade: ficam em fila, cada um com 60 s desde que chegou, e saem com a resposta, com o
///   tempo esgotado ou com um aviso da mesma sessão (resolvido no app).
public sealed class EstadoNotch(Func<DateTime> agora)
{
    public const double Duracao = 6, EsperaLaterais = 0.25, TempoPedido = 60, TempoConfirmacao = 1;

    private readonly List<(PedidoPermissao Pedido, DateTime Chegou)> _fila = [];
    private DateTime _inicio, _entrou, _confirmou;
    private bool _mouse;

    public ModoNotch Modo { get; private set; } = ModoNotch.Normal;
    public AvisoClaude? Aviso { get; private set; }

    /// Há um aviso que ainda não foi aberto (a lateral esquerda o mostra).
    public bool Pendente { get; private set; }

    /// As laterais estão abertas (mouse parado sobre o notch, fora do aviso e do pedido).
    public bool Laterais { get; private set; }

    /// O pedido que está na ficha (null fora do modo Permissao).
    public PedidoPermissao? PedidoAtual => Modo == ModoNotch.Permissao && !Confirmando && _fila.Count > 0 ? _fila[0].Pedido : null;

    /// Quando o pedido atual chegou (a barra de tempo conta a partir daí).
    public DateTime? ChegadaDoAtual => PedidoAtual is null ? null : _fila[0].Chegou;

    /// Pedidos esperando atrás do atual (o "+N" da ficha).
    public int NaFila => Math.Max(0, _fila.Count - (Confirmando ? 0 : 1));

    /// A ficha mostra a confirmação da resposta por 1 s.
    public bool Confirmando { get; private set; }
    public EscolhaPermissao? UltimaEscolha { get; private set; }

    /// O último pedido saiu sem resposta (a lateral esquerda mostra "Pedido sem resposta").
    public bool PedidoPerdido { get; private set; }

    public event Action? Mudou;

    /// Cada pedido sai uma vez: com a escolha ou com null (tempo esgotado, resolvido no app).
    public event Action<PedidoPermissao, EscolhaPermissao?>? Encerrado;

    public void Chegou(AvisoClaude aviso)
    {
        // Um aviso do Claude da sessão de um pedido aberto diz que ele foi resolvido no app. O de atualização é do próprio
        // Vitral e nunca resolve pedido (um Claude numa pasta "Vitral" teria a mesma sessão).
        if (aviso.Tipo != TipoAviso.Atualizacao)
            for (int i = _fila.Count - 1; i >= 0; i--)
                if (_fila[i].Pedido.Sessao == aviso.Sessao) Sair(i, null);
        Aviso = aviso;
        Pendente = true;
        if (_fila.Count > 0 || Confirmando) { Mudou?.Invoke(); return; } // o pedido de outra sessão continua na frente
        Laterais = false;
        _inicio = agora();
        Ir(ModoNotch.Aviso, forcar: true);
    }

    public void Pediu(PedidoPermissao pedido)
    {
        _fila.Add((pedido, agora()));
        PedidoPerdido = false;
        Laterais = false;
        if (Modo == ModoNotch.Permissao) { Mudou?.Invoke(); return; } // "+N"
        Ir(ModoNotch.Permissao, forcar: true);
    }

    /// O clique num botão da ficha; devolve o pedido respondido (o canal completa a resposta pelo Encerrado).
    public PedidoPermissao? Respondeu(EscolhaPermissao escolha)
    {
        if (PedidoAtual is not { } pedido) return null;
        Sair(0, escolha);
        Confirmando = true;
        UltimaEscolha = escolha;
        _confirmou = agora();
        Mudou?.Invoke();
        return pedido;
    }

    /// Chamado de tempos em tempos (a janela usa 250 ms enquanto há aviso, pedido ou mouse em cima).
    public void Tique()
    {
        var t = agora();
        bool perdeu = false;
        for (int i = _fila.Count - 1; i >= 0; i--)
            if ((t - _fila[i].Chegou).TotalSeconds >= TempoPedido) { Sair(i, null); perdeu = true; }
        if (Confirmando && (t - _confirmou).TotalSeconds >= TempoConfirmacao) { Confirmando = false; perdeu = false; Avancar(semResposta: false); }
        else if (perdeu && !Confirmando) Avancar(semResposta: true);
        else if (perdeu) Mudou?.Invoke();

        if (Modo == ModoNotch.Aviso && !_mouse && (t - _inicio).TotalSeconds >= Duracao) Ir(ModoNotch.Lembrete);
        else if (Modo is ModoNotch.Normal or ModoNotch.Lembrete && _mouse && !Laterais && (t - _entrou).TotalSeconds >= EsperaLaterais)
        {
            Laterais = true;
            Mudou?.Invoke();
        }
    }

    /// Depois de uma resposta ou de um pedido perdido: o próximo da fila, ou o fim do modo de pedido.
    private void Avancar(bool semResposta)
    {
        if (_fila.Count > 0) { Ir(ModoNotch.Permissao, forcar: true); return; }
        if (semResposta)
        {
            PedidoPerdido = true;
            Pendente = true;
            Ir(ModoNotch.Lembrete, forcar: true);
            return;
        }
        Ir(Pendente ? ModoNotch.Lembrete : ModoNotch.Normal, forcar: true);
    }

    private void Sair(int indice, EscolhaPermissao? escolha)
    {
        var pedido = _fila[indice].Pedido;
        _fila.RemoveAt(indice);
        Encerrado?.Invoke(pedido, escolha);
    }

    public void MouseEntrou()
    {
        _mouse = true;
        _entrou = agora();
    }

    public void MouseSaiu()
    {
        _mouse = false;
        _inicio = agora();
        if (!Laterais) return;
        Laterais = false;
        Mudou?.Invoke();
    }

    public AcaoNotch Clicou(ParteNotch parte = ParteNotch.Meio)
    {
        if (Modo == ModoNotch.Permissao) return AcaoNotch.Nenhuma; // na ficha, só os botões respondem
        // O aviso (ou o lembrete) de uma versão nova do Vitral reinicia; os do Claude (e o pedido perdido) abrem o Claude.
        var abrir = Aviso?.Tipo == TipoAviso.Atualizacao && Pendente && !PedidoPerdido ? AcaoNotch.Reiniciar : AcaoNotch.AbrirClaude;
        var acao = parte switch
        {
            ParteNotch.Esquerda => abrir,
            ParteNotch.Direita => AcaoNotch.AbrirNotificacoes,
            _ => Modo == ModoNotch.Aviso ? abrir : AcaoNotch.AbrirCalendario,
        };
        if (acao is AcaoNotch.AbrirClaude or AcaoNotch.Reiniciar) { Pendente = false; PedidoPerdido = false; }
        Ir(ModoNotch.Normal);
        return acao;
    }

    private void Ir(ModoNotch modo, bool forcar = false)
    {
        if (Modo == modo && !forcar) return;
        Modo = modo;
        Mudou?.Invoke();
    }
}

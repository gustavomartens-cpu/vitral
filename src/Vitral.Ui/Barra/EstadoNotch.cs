using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

public enum ModoNotch { Normal, Aviso, Lembrete, Permissao, Resposta }

public enum AcaoNotch { Nenhuma, AbrirClaude, AbrirCalendario, AbrirNotificacoes, Reiniciar }

/// Onde o clique caiu: no meio (relógio ou aviso) ou numa das laterais (spec do notch vivo §3.4).
public enum ParteNotch { Meio, Esquerda, Direita }

/// O notch vivo (spec do notch vivo §3.3 e §3.4; spec das permissões §4.2):
/// - o aviso fica 6 s (segurado com o mouse em cima) e recolhe para o lembrete;
/// - fora do aviso, o mouse parado 250 ms abre as laterais (Claude à esquerda, notificações à direita);
/// - pedidos de permissão têm prioridade: ficam em fila, cada um com 60 s desde que chegou, e saem com a resposta, com o
///   tempo esgotado ou com um aviso da mesma sessão (resolvido no app).
public sealed partial class EstadoNotch(Func<DateTime> agora)
{
    /// EsperaClique: um pedido que acabou de aparecer na ficha não aceita clique, para um clique dado no anterior não
    /// cair nele.
    public const double Duracao = 6, EsperaLaterais = 0.25, TempoPedido = 60, TempoConfirmacao = 1, EsperaClique = 0.5;

    private readonly List<(PedidoPermissao Pedido, DateTime Chegou)> _fila = [];
    private DateTime _inicio, _entrou, _confirmou, _mostrou;
    private bool _perdeuNaConfirmacao;
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
        // Com o id da sessão dos dois lados, só ele vale (várias conversas na mesma pasta); sem ele, o nome da pasta.
        var atual = PedidoAtual;
        if (aviso.Tipo != TipoAviso.Atualizacao)
            for (int i = _fila.Count - 1; i >= 0; i--)
            {
                var p = _fila[i].Pedido;
                if (p.SessaoId is not null && aviso.SessaoId is not null ? p.SessaoId == aviso.SessaoId : p.Sessao == aviso.Sessao) Sair(i, null);
            }
        if (atual is not null && PedidoAtual is { } seguinte && seguinte != atual) _mostrou = agora(); // outro tomou a ficha
        AvisoNasRespostas(aviso);
        Aviso = aviso;
        Pendente = true;
        if (aviso.Tipo != TipoAviso.Atualizacao) PedidoPerdido = false; // a lateral passa a mostrar o aviso novo
        // O pedido ou a ficha de outra sessão continuam na frente.
        if (_fila.Count > 0 || Confirmando || _respostas.Count > 0 || ConfirmandoResposta) { Mudou?.Invoke(); return; }
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
        Mostrar();
    }

    /// O clique num botão da ficha, para o pedido <paramref name="id"/> que estava na tela; devolve o pedido
    /// respondido (o canal completa a resposta pelo Encerrado). Recusa se outro pedido tomou a ficha ou se o atual
    /// acabou de aparecer.
    public PedidoPermissao? Respondeu(string id, EscolhaPermissao escolha)
    {
        if (PedidoAtual is not { } pedido || pedido.Id != id || (agora() - _mostrou).TotalSeconds < EsperaClique) return null;
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
        if (perdeu && Confirmando) _perdeuNaConfirmacao = true; // conta quando a confirmação acabar
        if (Confirmando && (t - _confirmou).TotalSeconds >= TempoConfirmacao)
        {
            Confirmando = false;
            var semResposta = _perdeuNaConfirmacao;
            _perdeuNaConfirmacao = false;
            Avancar(semResposta);
        }
        else if (perdeu && !Confirmando) Avancar(semResposta: true);
        else if (perdeu) Mudou?.Invoke();
        if (TiqueResposta(t) && Modo == ModoNotch.Resposta) Avancar(semResposta: false);

        if (Modo == ModoNotch.Aviso && !_mouse && (t - _inicio).TotalSeconds >= Duracao) Ir(ModoNotch.Lembrete);
        else if (Modo is ModoNotch.Normal or ModoNotch.Lembrete && _mouse && !Laterais && (t - _entrou).TotalSeconds >= EsperaLaterais)
        {
            Laterais = true;
            Mudou?.Invoke();
        }
    }

    /// Depois de uma resposta, de um pedido perdido ou de uma ficha encerrada: o próximo pedido, a próxima ficha, ou o
    /// fim (lembrete se há algo pendente).
    private void Avancar(bool semResposta)
    {
        if (_fila.Count > 0) { Mostrar(); return; }
        if (semResposta)
        {
            PedidoPerdido = true;
            Pendente = true;
        }
        if (_respostas.Count > 0) { MostrarResposta(); return; }
        Ir(Pendente ? ModoNotch.Lembrete : ModoNotch.Normal, forcar: true);
    }

    /// O Claude largou o pedido (respondido no app, o gancho fechou): sai da fila sem contar como sem resposta.
    public void Desistiu(string id)
    {
        var i = _fila.FindIndex(x => x.Pedido.Id == id);
        if (i < 0) return;
        bool eraOAtual = PedidoAtual?.Id == id;
        Sair(i, null);
        if (eraOAtual) Avancar(semResposta: false);
        else Mudou?.Invoke(); // o "+N"
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
        if (Modo is ModoNotch.Permissao or ModoNotch.Resposta) return AcaoNotch.Nenhuma; // nas fichas, só os botões respondem
        // O aviso (ou o lembrete) de uma versão nova do Vitral reinicia; os do Claude (e o pedido perdido) abrem o Claude.
        var abrir = Aviso?.Tipo == TipoAviso.Atualizacao && Pendente && !PedidoPerdido ? AcaoNotch.Reiniciar : AcaoNotch.AbrirClaude;
        var acao = parte switch
        {
            ParteNotch.Esquerda => abrir,
            ParteNotch.Direita => AcaoNotch.AbrirNotificacoes,
            _ => Modo == ModoNotch.Aviso ? abrir : AcaoNotch.AbrirCalendario,
        };
        // O pedido perdido em cima de uma atualização pendente: abrir o Claude limpa só o pedido; o lembrete da
        // atualização continua.
        if (acao == AcaoNotch.AbrirClaude && PedidoPerdido && Aviso?.Tipo == TipoAviso.Atualizacao) PedidoPerdido = false;
        else if (acao is AcaoNotch.AbrirClaude or AcaoNotch.Reiniciar) { Pendente = false; PedidoPerdido = false; }
        Ir(acao == AcaoNotch.AbrirClaude && Pendente ? ModoNotch.Lembrete : ModoNotch.Normal);
        return acao;
    }

    /// O primeiro da fila vai para a ficha (e só aceita clique depois da EsperaClique).
    private void Mostrar()
    {
        _mostrou = agora();
        Ir(ModoNotch.Permissao, forcar: true);
    }

    private void Ir(ModoNotch modo, bool forcar = false)
    {
        if (Modo == modo && !forcar) return;
        Modo = modo;
        Mudou?.Invoke();
    }
}

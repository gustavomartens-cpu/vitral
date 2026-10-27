using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

/// A ficha de resposta ao Claude (spec da resposta §4.1): fila própria, atrás dos pedidos de permissão e na frente dos
/// avisos; cada ficha tem 60 s desde que chegou, ou 15 min desde o primeiro caractere digitado. Sem texto (tempo, Esc,
/// teto), a sessão vai para as <see cref="Pendencias"/>.
public sealed partial class EstadoNotch
{
    public const double TempoResposta = 60, TetoDigitando = 15 * 60;

    private sealed class Ficha(PedidoResposta pedido, DateTime chegou)
    {
        public PedidoResposta Pedido { get; } = pedido;
        public DateTime Chegou { get; } = chegou;
        public DateTime? Digitou { get; set; }
        public string Rascunho { get; set; } = "";
    }

    private readonly List<Ficha> _respostas = [];
    private DateTime _mostrouResposta;

    public Pendencias Pendencias { get; } = new();

    /// A ficha que está no notch (null fora do modo Resposta e durante a confirmação do envio).
    public PedidoResposta? RespostaAtual => FichaAtual?.Pedido;

    private Ficha? FichaAtual => Modo == ModoNotch.Resposta && !ConfirmandoResposta && _respostas.Count > 0 ? _respostas[0] : null;

    /// Quando a ficha atual chegou (a barra de 60 s conta daí).
    public DateTime? ChegadaDaResposta => FichaAtual?.Chegou;

    /// O Gustavo começou a digitar na ficha atual: a barra para e vale o teto de 15 min.
    public bool Digitando => FichaAtual?.Digitou is not null;

    /// O texto da ficha atual (guardado quando um pedido de permissão passa à frente).
    public string Rascunho => FichaAtual?.Rascunho ?? "";

    /// Fichas esperando atrás da atual (o "+N").
    public int NaFilaResposta => Math.Max(0, _respostas.Count - (ConfirmandoResposta ? 0 : 1));

    /// A ficha mostra "✓ Enviado" por 1 s.
    public bool ConfirmandoResposta { get; private set; }

    /// Cada ficha sai uma vez: com o texto ou com null (tempo, Esc, teto, desistência, aviso da mesma sessão).
    public event Action<PedidoResposta, string?>? RespostaEncerrada;

    public void PediuResposta(PedidoResposta pedido)
    {
        Pendencias.SaiuDaSessao(pedido.SessaoId, pedido.Sessao);
        _respostas.Add(new Ficha(pedido, agora()));
        Laterais = false;
        if (Modo is ModoNotch.Permissao or ModoNotch.Resposta || Confirmando || ConfirmandoResposta) { Mudou?.Invoke(); return; } // "+N"
        MostrarResposta();
    }

    public void Digitou(string id, string texto)
    {
        if (FichaAtual is not { } f || f.Pedido.Id != id) return;
        f.Rascunho = texto;
        if (texto.Length > 0) Comecou(f);
    }

    /// O clique na caixa: o Gustavo vai responder; a barra para já aqui (senão a ficha venceria entre o clique e o
    /// primeiro caractere, com a caixa em foco).
    public void Focou(string id)
    {
        if (FichaAtual is { } f && f.Pedido.Id == id) Comecou(f);
    }

    private void Comecou(Ficha f)
    {
        if (f.Digitou is not null) return;
        f.Digitou = agora();
        Mudou?.Invoke(); // a barra para
    }

    /// "Enviar" ou Enter, para a ficha <paramref name="id"/> que estava na tela; recusa texto em branco, outra ficha ou
    /// uma que acabou de aparecer.
    public bool Enviou(string id)
    {
        if (FichaAtual is not { } f || f.Pedido.Id != id || string.IsNullOrWhiteSpace(f.Rascunho)
            || (agora() - _mostrouResposta).TotalSeconds < EsperaClique) return false;
        _respostas.Remove(f);
        RespostaEncerrada?.Invoke(f.Pedido, f.Rascunho.Trim());
        ConfirmandoResposta = true;
        _confirmou = agora();
        Mudou?.Invoke();
        return true;
    }

    /// Esc: a sessão é solta e vai para as pendências.
    public void Cancelou(string id)
    {
        if (FichaAtual is not { } f || f.Pedido.Id != id) return;
        Encerrar(f, pendente: true);
        Avancar(semResposta: false);
    }

    /// "Descartar": não há o que responder (o Claude só contou o que fez). A sessão é solta e sai de vez, sem ir para
    /// as pendências, e a pendência antiga dela também sai.
    public void Descartou(string id)
    {
        if (FichaAtual is not { } f || f.Pedido.Id != id) return;
        Encerrar(f, pendente: false);
        Pendencias.SaiuDaSessao(f.Pedido.SessaoId, f.Pedido.Sessao);
        Avancar(semResposta: false);
    }

    /// O mensageiro fechou (o Claude largou a sessão): sai sem ir para as pendências; o aviso seguinte diz o que houve.
    public void DesistiuResposta(string id)
    {
        if (_respostas.Find(x => x.Pedido.Id == id) is not { } f) return;
        bool eraAtual = f == FichaAtual;
        Encerrar(f, pendente: false);
        if (eraAtual) Avancar(semResposta: false);
        else Mudou?.Invoke();
    }

    /// Sem notch visível (barra desligada, tela cheia): não há ficha; a sessão vai direto para as pendências.
    public void SemNotch(PedidoResposta pedido)
    {
        Pendencias.Entrou(ComoPendencia(pedido));
        Pendente = true; // o ponto de lembrete, quando o notch voltar
    }

    private void Encerrar(Ficha f, bool pendente)
    {
        _respostas.Remove(f);
        RespostaEncerrada?.Invoke(f.Pedido, null);
        if (pendente) Pendencias.Entrou(ComoPendencia(f.Pedido));
    }

    private static Pendencias.Pendencia ComoPendencia(PedidoResposta p) => new(p.SessaoId, p.Sessao, TipoAviso.Terminou, p.Hora, p.Completo, p.AppId);

    /// Um aviso chegou: a ficha da mesma sessão sai (resolvida no app), as pendências dela também, e um "esperando
    /// você" fora do app vira pendência. O aviso de atualização é do Vitral e não mexe em nada disso.
    private void AvisoNasRespostas(AvisoClaude aviso)
    {
        if (aviso.Tipo == TipoAviso.Atualizacao) return;
        var antes = RespostaAtual;
        foreach (var f in _respostas.Where(f => Pendencias.MesmaSessao(f.Pedido.SessaoId, f.Pedido.Sessao, aviso.SessaoId, aviso.Sessao)).ToList())
            Encerrar(f, pendente: false);
        Pendencias.SaiuDaSessao(aviso.SessaoId, aviso.Sessao);
        if (aviso is { Tipo: TipoAviso.Esperando, ForaDoApp: true })
            Pendencias.Entrou(new(aviso.SessaoId, aviso.Sessao, TipoAviso.Esperando, agora(), aviso.Completo ?? aviso.Texto, aviso.AppId));
        if (antes is not null && _respostas.Count > 0 && _fila.Count == 0 && RespostaAtual != antes) MostrarResposta(); // outra tomou a ficha
    }

    /// Do Tique: fichas vencidas e o fim da confirmação do envio. Devolve true quando a ficha da frente saiu.
    private bool TiqueResposta(DateTime t)
    {
        var atual = FichaAtual;
        bool saiuAAtual = false, venceu = false;
        foreach (var f in _respostas.ToList())
        {
            var limite = f.Digitou is { } d ? d.AddSeconds(TetoDigitando) : f.Chegou.AddSeconds(TempoResposta);
            if (t < limite) continue;
            saiuAAtual |= f == atual;
            venceu = true;
            Encerrar(f, pendente: true);
            Pendente = true; // o ponto de lembrete avisa que há sessão esperando
        }
        if (ConfirmandoResposta && (t - _confirmou).TotalSeconds >= TempoConfirmacao)
        {
            ConfirmandoResposta = false;
            return true;
        }
        if (venceu && !saiuAAtual) Mudou?.Invoke();
        return saiuAAtual;
    }

    /// A primeira ficha da fila vai para o notch (e só aceita Enviar depois da EsperaClique).
    private void MostrarResposta()
    {
        _mostrouResposta = agora();
        Ir(ModoNotch.Resposta, forcar: true);
    }
}

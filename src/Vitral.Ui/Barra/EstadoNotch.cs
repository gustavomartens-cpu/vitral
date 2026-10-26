using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

public enum ModoNotch { Normal, Aviso, Lembrete }

public enum AcaoNotch { Nenhuma, AbrirClaude, AbrirCalendario, AbrirNotificacoes }

/// Onde o clique caiu: no meio (relógio ou aviso) ou numa das laterais (spec do notch vivo §3.4).
public enum ParteNotch { Meio, Esquerda, Direita }

/// O notch vivo (spec do notch vivo §3.3 e §3.4): o aviso fica 6 s (segurado com o mouse em cima) e recolhe para o
/// lembrete; fora do aviso, o mouse parado 250 ms abre as laterais (Claude à esquerda, notificações à direita).
public sealed class EstadoNotch(Func<DateTime> agora)
{
    public const double Duracao = 6, EsperaLaterais = 0.25;

    private DateTime _inicio, _entrou;
    private bool _mouse;

    public ModoNotch Modo { get; private set; } = ModoNotch.Normal;
    public AvisoClaude? Aviso { get; private set; }

    /// Há um aviso que ainda não foi aberto (a lateral esquerda o mostra).
    public bool Pendente { get; private set; }

    /// As laterais estão abertas (mouse parado sobre o notch, fora do aviso).
    public bool Laterais { get; private set; }

    public event Action? Mudou;

    public void Chegou(AvisoClaude aviso)
    {
        Aviso = aviso;
        Pendente = true;
        Laterais = false;
        _inicio = agora();
        Ir(ModoNotch.Aviso, forcar: true);
    }

    /// Chamado de tempos em tempos (a janela usa 250 ms enquanto há aviso ou mouse em cima).
    public void Tique()
    {
        if (Modo == ModoNotch.Aviso && !_mouse && (agora() - _inicio).TotalSeconds >= Duracao) Ir(ModoNotch.Lembrete);
        else if (Modo != ModoNotch.Aviso && _mouse && !Laterais && (agora() - _entrou).TotalSeconds >= EsperaLaterais)
        {
            Laterais = true;
            Mudou?.Invoke();
        }
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
        var acao = parte switch
        {
            ParteNotch.Esquerda => AcaoNotch.AbrirClaude,
            ParteNotch.Direita => AcaoNotch.AbrirNotificacoes,
            _ => Modo == ModoNotch.Aviso ? AcaoNotch.AbrirClaude : AcaoNotch.AbrirCalendario,
        };
        if (acao == AcaoNotch.AbrirClaude) Pendente = false;
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

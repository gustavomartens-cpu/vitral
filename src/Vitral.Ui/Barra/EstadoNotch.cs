using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

public enum ModoNotch { Normal, Aviso, Lembrete }

public enum AcaoNotch { Nenhuma, AbrirClaude, AbrirCalendario }

/// O notch vivo (spec do notch vivo §3.3): o aviso fica 6 s (segurado com o mouse em cima) e recolhe para o lembrete;
/// clique no aviso abre o Claude, no resto abre o calendário.
public sealed class EstadoNotch(Func<DateTime> agora)
{
    public const double Duracao = 6;

    private DateTime _inicio;
    private bool _mouse;

    public ModoNotch Modo { get; private set; } = ModoNotch.Normal;
    public AvisoClaude? Aviso { get; private set; }
    public event Action? Mudou;

    public void Chegou(AvisoClaude aviso)
    {
        Aviso = aviso;
        _inicio = agora();
        Ir(ModoNotch.Aviso, forcar: true);
    }

    /// Chamado de tempos em tempos (a janela usa 250 ms).
    public void Tique()
    {
        if (Modo == ModoNotch.Aviso && !_mouse && (agora() - _inicio).TotalSeconds >= Duracao) Ir(ModoNotch.Lembrete);
    }

    public void MouseEntrou() => _mouse = true;

    public void MouseSaiu()
    {
        _mouse = false;
        _inicio = agora();
    }

    public AcaoNotch Clicou()
    {
        var acao = Modo == ModoNotch.Aviso ? AcaoNotch.AbrirClaude : AcaoNotch.AbrirCalendario;
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

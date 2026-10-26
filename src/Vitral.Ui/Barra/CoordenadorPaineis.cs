using CommunityToolkit.Mvvm.ComponentModel;

namespace Vitral.Ui.Barra;

public enum Painel { Nenhum, Calendario, Wifi, Volume, Bateria, Midia, Icones }

/// Qual painel da barra do topo está aberto: um por vez (spec v2 §4).
/// O clique na ilha tira o foco do painel antes de chegar como "alternar": por isso o mesmo painel fechado por
/// perda de foco há menos de <see cref="JanelaReaberturaMs"/> não reabre (o clique queria fechar).
public sealed partial class CoordenadorPaineis : ObservableObject
{
    public const int JanelaReaberturaMs = 300;

    [ObservableProperty] private Painel _aberto;

    /// O painel aberto desceu só com o mouse (a mídia, ver <see cref="PassarMouse"/>): mostrado sem tirar o foco e
    /// fechado pelo mouse, até um clique fixá-lo.
    [ObservableProperty] private bool _semFoco;

    private Painel _fechadoPorFoco;
    private DateTime _quandoFechouPorFoco;

    public void Alternar(Painel painel, DateTime agora)
    {
        if (painel == Painel.Nenhum) return;
        if (SemFoco && Aberto == painel)
        {
            Fixar(); // o clique na ilha de um painel aberto pelo mouse quer usá-lo, não fechá-lo
            return;
        }
        if (painel == _fechadoPorFoco && (agora - _quandoFechouPorFoco).TotalMilliseconds < JanelaReaberturaMs)
        {
            _fechadoPorFoco = Painel.Nenhum;
            return;
        }
        Ir(Aberto == painel ? Painel.Nenhum : painel);
    }

    /// Abre pelo mouse, só se nenhum painel estiver aberto (passar pela ilha não fecha o que se está usando).
    public bool AbrirSemFoco(Painel painel)
    {
        if (painel == Painel.Nenhum || Aberto != Painel.Nenhum) return false;
        SemFoco = true; // antes do Aberto: quem mostra a janela lê os dois
        Aberto = painel;
        return true;
    }

    /// O painel aberto pelo mouse foi fixado por um clique: quem mostra a janela lhe dá o foco.
    public event Action? Fixou;

    /// Um clique no painel aberto pelo mouse: passa a ser como os outros (foco).
    public void Fixar()
    {
        if (!SemFoco) return;
        SemFoco = false;
        Fixou?.Invoke();
    }

    /// O mouse saiu: fecha só se o painel ainda estiver aberto pelo mouse.
    public void FecharSemFoco(Painel painel)
    {
        if (SemFoco && Aberto == painel) Ir(Painel.Nenhum);
    }

    private void Ir(Painel painel)
    {
        SemFoco = false;
        Aberto = painel;
    }

    public void FecharPorFoco(Painel painel, DateTime agora)
    {
        if (Aberto != painel) return;
        _fechadoPorFoco = painel;
        _quandoFechouPorFoco = agora;
        Ir(Painel.Nenhum);
    }

    public void FecharTodos() => Ir(Painel.Nenhum);
}

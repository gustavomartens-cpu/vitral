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

    private Painel _fechadoPorFoco;
    private DateTime _quandoFechouPorFoco;

    public void Alternar(Painel painel, DateTime agora)
    {
        if (painel == Painel.Nenhum) return;
        if (painel == _fechadoPorFoco && (agora - _quandoFechouPorFoco).TotalMilliseconds < JanelaReaberturaMs)
        {
            _fechadoPorFoco = Painel.Nenhum;
            return;
        }
        Aberto = Aberto == painel ? Painel.Nenhum : painel;
    }

    public void FecharPorFoco(Painel painel, DateTime agora)
    {
        if (Aberto != painel) return;
        _fechadoPorFoco = painel;
        _quandoFechouPorFoco = agora;
        Aberto = Painel.Nenhum;
    }

    public void FecharTodos() => Aberto = Painel.Nenhum;
}

namespace Vitral.Ui.Barra;

public enum AcaoPassar { Nenhuma, Abrir, Fechar }

/// Abrir o painel da mídia só de passar o mouse (pedido do Gustavo, 26/10): o mouse parado 300 ms na ilha abre o
/// painel sem tirar o foco de onde se está digitando; ele fecha 400 ms depois de o mouse sair da ilha e do painel (a
/// folga deixa descer da ilha ao painel). Um clique fixa o painel, que passa a fechar como os outros (clique fora);
/// fechado por outro caminho, só reabre quando o mouse sair e entrar de novo na ilha.
public sealed class PassarMouse(Func<DateTime> agora)
{
    public const double EsperaAbrir = 0.3, FolgaFechar = 0.4;

    private bool _naIlha, _noPainel, _armado;
    private DateTime _entrou, _saiu;

    /// O painel está aberto por este passar (e ainda não foi fixado).
    public bool Aberto { get; private set; }

    /// Nada a vigiar: o mouse está fora da ilha e do painel, e o painel não está aberto pelo mouse.
    public bool Ocioso => !Aberto && !_naIlha && !_noPainel;

    public void Entrou(bool painel)
    {
        if (painel) { _noPainel = true; return; }
        if (!_naIlha) { _entrou = agora(); _armado = !Aberto; }
        _naIlha = true;
    }

    public void Saiu(bool painel)
    {
        if (painel) _noPainel = false; else _naIlha = false;
        _saiu = agora();
    }

    /// Um clique no painel ou no título: o painel passa a fechar pelo foco, não pelo mouse.
    public void Fixou() => Largar();

    /// O painel fechou por outro caminho (Esc, outro painel, a mídia sumiu).
    public void Fechou() => Largar();

    private void Largar()
    {
        Aberto = false;
        _armado = false;
    }

    /// Chamado de tempos em tempos (100 ms) enquanto o mouse está na ilha ou no painel, ou o painel está aberto.
    public AcaoPassar Tique()
    {
        var t = agora();
        if (!Aberto)
        {
            if (!_armado || !_naIlha || (t - _entrou).TotalSeconds < EsperaAbrir) return AcaoPassar.Nenhuma;
            Aberto = true;
            _armado = false;
            return AcaoPassar.Abrir;
        }
        if (_naIlha || _noPainel || (t - _saiu).TotalSeconds < FolgaFechar) return AcaoPassar.Nenhuma;
        Aberto = false;
        return AcaoPassar.Fechar;
    }
}

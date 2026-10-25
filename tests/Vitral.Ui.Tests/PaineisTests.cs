using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PaineisTests
{
    private static readonly DateTime T = new(2026, 10, 25, 10, 0, 0);

    [Fact]
    public void Abrir_outro_fecha_o_anterior()
    {
        var c = new CoordenadorPaineis();
        c.Alternar(Painel.Calendario, T);
        c.Alternar(Painel.Volume, T);
        Assert.Equal(Painel.Volume, c.Aberto);
    }

    [Fact]
    public void Alternar_o_mesmo_fecha()
    {
        var c = new CoordenadorPaineis();
        c.Alternar(Painel.Wifi, T);
        c.Alternar(Painel.Wifi, T);
        Assert.Equal(Painel.Nenhum, c.Aberto);
    }

    [Fact]
    public void Reabrir_logo_apos_perder_o_foco_nao_reabre()
    {
        var c = new CoordenadorPaineis();
        c.Alternar(Painel.Volume, T);
        c.FecharPorFoco(Painel.Volume, T);
        c.Alternar(Painel.Volume, T.AddMilliseconds(100));
        Assert.Equal(Painel.Nenhum, c.Aberto);
        c.Alternar(Painel.Volume, T.AddMilliseconds(400));
        Assert.Equal(Painel.Volume, c.Aberto);
    }

    [Fact]
    public void Outro_painel_logo_apos_perder_o_foco_abre()
    {
        var c = new CoordenadorPaineis();
        c.Alternar(Painel.Volume, T);
        c.FecharPorFoco(Painel.Volume, T);
        c.Alternar(Painel.Bateria, T.AddMilliseconds(50));
        Assert.Equal(Painel.Bateria, c.Aberto);
    }

    [Fact]
    public void Perder_foco_de_painel_que_nao_esta_aberto_nao_fecha_o_atual()
    {
        var c = new CoordenadorPaineis();
        c.Alternar(Painel.Volume, T);
        c.Alternar(Painel.Calendario, T);
        c.FecharPorFoco(Painel.Volume, T);
        Assert.Equal(Painel.Calendario, c.Aberto);
    }

    [Fact]
    public void Fechar_todos()
    {
        var c = new CoordenadorPaineis();
        c.Alternar(Painel.Midia, T);
        c.FecharTodos();
        Assert.Equal(Painel.Nenhum, c.Aberto);
    }
}

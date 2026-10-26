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
    public void Abrir_sem_foco_so_quando_nada_esta_aberto()
    {
        var c = new CoordenadorPaineis();
        Assert.True(c.AbrirSemFoco(Painel.Midia));
        Assert.Equal(Painel.Midia, c.Aberto);
        Assert.True(c.SemFoco);

        var d = new CoordenadorPaineis();
        d.Alternar(Painel.Calendario, T);
        Assert.False(d.AbrirSemFoco(Painel.Midia));
        Assert.Equal(Painel.Calendario, d.Aberto);
    }

    [Fact]
    public void Clique_na_ilha_do_painel_aberto_sem_foco_fixa_em_vez_de_fechar()
    {
        var c = new CoordenadorPaineis();
        var fixou = 0;
        c.Fixou += () => fixou++;
        c.AbrirSemFoco(Painel.Midia);
        c.Alternar(Painel.Midia, T);
        Assert.Equal(Painel.Midia, c.Aberto);
        Assert.False(c.SemFoco);
        Assert.Equal(1, fixou);
        c.FecharTodos();
        Assert.Equal(1, fixou); // fechar não é fixar
    }

    [Fact]
    public void Fechar_sem_foco_nao_fecha_o_painel_fixado()
    {
        var c = new CoordenadorPaineis();
        c.AbrirSemFoco(Painel.Midia);
        c.Fixar();
        c.FecharSemFoco(Painel.Midia);
        Assert.Equal(Painel.Midia, c.Aberto);

        var d = new CoordenadorPaineis();
        d.AbrirSemFoco(Painel.Midia);
        d.FecharSemFoco(Painel.Midia);
        Assert.Equal(Painel.Nenhum, d.Aberto);
    }

    [Fact]
    public void Abrir_outro_pelo_clique_tira_o_sem_foco()
    {
        var c = new CoordenadorPaineis();
        c.AbrirSemFoco(Painel.Midia);
        c.Alternar(Painel.Volume, T);
        Assert.Equal(Painel.Volume, c.Aberto);
        Assert.False(c.SemFoco);
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

using Vitral.Ui.Barra;
using Vitral.Ui.Doca;

namespace Vitral.Ui.Tests;

public sealed class AutoOcultarDocaTests
{
    private static readonly DateTime T0 = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    private static DateTime Ms(int ms) => T0.AddMilliseconds(ms);

    private static AutoOcultarDoca Visivel()
    {
        var a = new AutoOcultarDoca();
        a.Reiniciar(T0, visivel: true);
        return a;
    }

    private static AutoOcultarDoca Escondida()
    {
        var a = new AutoOcultarDoca();
        a.Reiniciar(T0, visivel: false);
        return a;
    }

    private static AcaoDoca Nada(AutoOcultarDoca a, int ms) => a.Conferir(Ms(ms), false, false, false, false);

    [Fact]
    public void Some_depois_de_500ms_sem_mouse()
    {
        var a = Visivel();
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 499));
        Assert.Equal(AcaoDoca.Esconder, Nada(a, 500));
        Assert.False(a.Visivel);
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 600));
    }

    [Fact]
    public void Mouse_na_doca_adia_o_sumir()
    {
        var a = Visivel();
        Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(400), false, true, false, false));
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 800));
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 899));
        Assert.Equal(AcaoDoca.Esconder, Nada(a, 900));
    }

    [Fact]
    public void Mouse_na_borda_tambem_segura_a_doca()
    {
        var a = Visivel();
        Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(400), true, false, false, false));
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 800));
        Assert.Equal(AcaoDoca.Esconder, Nada(a, 900));
    }

    [Fact]
    public void Trava_impede_sumir()
    {
        var a = Visivel();
        for (int ms = 0; ms <= 2000; ms += 100) Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(ms), false, false, true, false));
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 2499));
        Assert.Equal(AcaoDoca.Esconder, Nada(a, 2500));
    }

    [Fact]
    public void Aparece_com_100ms_na_borda()
    {
        var a = Escondida();
        Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(1000), true, false, false, false));
        Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(1099), true, false, false, false));
        Assert.Equal(AcaoDoca.Mostrar, a.Conferir(Ms(1100), true, false, false, false));
        Assert.True(a.Visivel);
    }

    [Fact]
    public void Sair_da_borda_zera_a_espera()
    {
        var a = Escondida();
        Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(1000), true, false, false, false));
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 1050));
        Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(1060), true, false, false, false));
        Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(1150), true, false, false, false));
        Assert.Equal(AcaoDoca.Mostrar, a.Conferir(Ms(1160), true, false, false, false));
    }

    [Fact]
    public void Tela_cheia_nao_mostra()
    {
        var a = Escondida();
        for (int ms = 0; ms <= 1000; ms += 50) Assert.Equal(AcaoDoca.Nenhuma, a.Conferir(Ms(ms), true, false, false, true));
        Assert.False(a.Visivel);
    }

    [Fact]
    public void Aparecer_recomeca_a_contagem_para_sumir()
    {
        var a = Escondida();
        a.Conferir(Ms(1000), true, false, false, false);
        Assert.Equal(AcaoDoca.Mostrar, a.Conferir(Ms(1100), true, false, false, false));
        // O mouse saiu logo depois de aparecer: some 500 ms após a última vez em que esteve na doca ou na borda.
        Assert.Equal(AcaoDoca.Nenhuma, Nada(a, 1599));
        Assert.Equal(AcaoDoca.Esconder, Nada(a, 1600));
    }

    [Fact]
    public void NaBorda_e_SobreDoca()
    {
        var monitor = new Retangulo(0, 0, 1920, 1080);
        Assert.True(AutoOcultarDoca.NaBorda(500, 1078, monitor));
        Assert.True(AutoOcultarDoca.NaBorda(500, 1079, monitor));
        Assert.False(AutoOcultarDoca.NaBorda(500, 1077, monitor));
        Assert.False(AutoOcultarDoca.NaBorda(-5, 1079, monitor)); // outro monitor à esquerda

        var doca = new Retangulo(700, 1000, 520, 54);
        Assert.True(AutoOcultarDoca.SobreDoca(710, 1020, doca, monitor));
        Assert.True(AutoOcultarDoca.SobreDoca(710, 1079, doca, monitor)); // o vão abaixo da flutuante conta
        Assert.False(AutoOcultarDoca.SobreDoca(690, 1020, doca, monitor));
        Assert.False(AutoOcultarDoca.SobreDoca(710, 990, doca, monitor));
    }
}

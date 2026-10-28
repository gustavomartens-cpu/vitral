using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PosicaoIlhasTests
{
    [Fact]
    public void Monitor_na_origem()
    {
        var (midia, relogio, status) = PosicaoIlhas.Calcular(new Retangulo(0, 0, 1920, 1032), 300, 140, 360);
        Assert.Equal(new Retangulo(12, 8, 300, 30), midia);
        Assert.Equal(new Retangulo(890, -10, 140, 44), relogio);
        Assert.Equal(new Retangulo(1548, 8, 360, 30), status);
    }

    [Fact]
    public void Monitor_com_origem_negativa()
    {
        var (midia, relogio, status) = PosicaoIlhas.Calcular(new Retangulo(-1080, -477, 1080, 1920), 300, 140, 360);
        Assert.Equal(new Retangulo(-1068, -469, 300, 30), midia);
        Assert.Equal(new Retangulo(-610, -487, 140, 44), relogio);
        Assert.Equal(new Retangulo(-372, -469, 360, 30), status);
    }

    [Fact]
    public void Painel_da_direita_alinha_pela_ilha()
    {
        var r = PosicaoIlhas.Painel(new Retangulo(1548, 8, 360, 30), 280, 200, AlinhamentoPainel.Direita, new Retangulo(0, 0, 1920, 1080));
        Assert.Equal(new Retangulo(1628, 44, 280, 200), r);
    }

    [Fact]
    public void Painel_centralizado_no_notch()
    {
        var r = PosicaoIlhas.Painel(new Retangulo(890, -10, 140, 44), 248, 260, AlinhamentoPainel.Centro, new Retangulo(0, 0, 1920, 1080));
        Assert.Equal(new Retangulo(836, 40, 248, 260), r);
    }

    [Fact]
    public void Painel_nao_sai_do_monitor()
    {
        var r = PosicaoIlhas.Painel(new Retangulo(12, 8, 100, 30), 260, 300, AlinhamentoPainel.Direita, new Retangulo(0, 0, 1920, 1080));
        Assert.Equal(12, r.X);
    }

    [Fact]
    public void Doca_centralizada_acima_da_borda() =>
        Assert.Equal(new Retangulo(1080, 1386, 400, 46), PosicaoIlhas.Doca(new Retangulo(0, 0, 2560, 1440), 400));

    [Fact]
    public void Miniaturas_sobem_do_botao_centralizadas_e_presas_no_monitor()
    {
        var monitor = new Retangulo(0, 0, 1000, 800);
        var doca = PosicaoIlhas.Doca(monitor, 300); // topo em 746
        Assert.Equal(new Retangulo(400, 636, 200, 100), PosicaoIlhas.Miniaturas(doca, 500, 200, 100, monitor));
        Assert.Equal(12, PosicaoIlhas.Miniaturas(doca, 30, 200, 100, monitor).X);
        Assert.Equal(1000 - 12 - 200, PosicaoIlhas.Miniaturas(doca, 990, 200, 100, monitor).X);
    }

    private static Core.Formas.ValoresFormas F(Core.Perfis.FormasPerfil? f) =>
        Core.Formas.ResolverFormas.Resolver(f, Core.Perfis.CantosParte.Redondos);

    private static readonly Retangulo Tela = new(0, 0, 1920, 1080);

    [Fact]
    public void Medidas_padrao_iguais_as_constantes()
    {
        var v = F(null);
        Assert.Equal((PosicaoIlhas.Topo, PosicaoIlhas.Borda, PosicaoIlhas.Altura), (v.Ilhas.Topo, v.Ilhas.Borda, v.Ilhas.Altura));
        Assert.Equal(PosicaoIlhas.Reserva, v.ReservaTopo);
        Assert.Equal((PosicaoIlhas.DocaAltura, PosicaoIlhas.DocaFolga, PosicaoIlhas.ReservaDoca), (v.Doca.Altura, v.Doca.Folga, v.Doca.Reserva));
        Assert.Equal((PosicaoIlhas.NotchSobra, NotchMedidas.AlturaNormal, NotchMedidas.RaioNormal, FormaNotch.Orelha),
            (v.Notch.Sobra, v.Notch.Altura, v.Notch.Raio, v.Notch.Orelha));
        Assert.Equal(PosicaoIlhas.Calcular(Tela, 300, 140, 360), PosicaoIlhas.Calcular(Tela, 300, 140, 360, v.Ilhas, v.Notch));
        Assert.Equal(PosicaoIlhas.Doca(Tela, 500), PosicaoIlhas.Doca(Tela, 500, v.Doca));
    }

    [Fact]
    public void Ilhas_encostadas_nas_bordas()
    {
        var v = F(new(Ilhas: new(Distancia: Core.Perfis.DistanciaIlhas.Encostadas)));
        var (midia, _, status) = PosicaoIlhas.Calcular(Tela, 300, 140, 360, v.Ilhas, v.Notch);
        Assert.Equal(new Retangulo(0, 0, 300, 30), midia);
        Assert.Equal(new Retangulo(1560, 0, 360, 30), status);
    }

    [Fact]
    public void Barra_unica_de_lado_a_lado()
    {
        Assert.Equal(new Retangulo(12, 8, 1896, 30), PosicaoIlhas.Barra(Tela, F(null).Ilhas));
        var encostada = F(new(Ilhas: new(Core.Perfis.DisposicaoIlhas.BarraUnica, Core.Perfis.DistanciaIlhas.Encostadas)));
        Assert.Equal(new Retangulo(0, 0, 1920, 30), PosicaoIlhas.Barra(Tela, encostada.Ilhas));
    }

    [Fact]
    public void Doca_corrida_ocupa_a_largura()
    {
        var v = F(new(new(Core.Perfis.FormatoDoca.Corrida, Core.Perfis.TamanhoIcones.Grande)));
        Assert.Equal(new Retangulo(0, 1024, 1920, 56), PosicaoIlhas.Doca(Tela, 500, v.Doca));
        var pequena = F(new(new(Icones: Core.Perfis.TamanhoIcones.Pequeno)));
        Assert.Equal(new Retangulo(710, 1034, 500, 38), PosicaoIlhas.Doca(Tela, 500, pequena.Doca));
    }

    [Fact]
    public void Notch_ilha_solto_da_borda()
    {
        var v = F(new(Notch: new(Core.Perfis.FormatoNotch.Ilha)));
        var (_, relogio, _) = PosicaoIlhas.Calcular(Tela, 300, 140, 360, v.Ilhas, v.Notch);
        Assert.Equal(new Retangulo(890, 6, 140, 34), relogio);
        var grande = F(new(Notch: new(Tamanho: Core.Perfis.TamanhoNotch.Grande)));
        Assert.Equal(new Retangulo(890, -10, 140, 50), PosicaoIlhas.Calcular(Tela, 300, 140, 360, grande.Ilhas, grande.Notch).Relogio);
    }
}

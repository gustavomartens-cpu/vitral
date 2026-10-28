using Vitral.Core.Formas;
using Vitral.Core.Perfis;

namespace Vitral.Core.Tests;

public sealed class FormasTests
{
    private static ValoresFormas R(FormasPerfil? f, CantosParte cantos = CantosParte.Redondos) => ResolverFormas.Resolver(f, cantos);

    [Fact]
    public void Tudo_nulo_da_as_medidas_de_hoje()
    {
        var v = R(null);
        Assert.Equal(new ValoresDoca(FormatoDoca.Flutuante, 30, 24, 40, 44, 46, 8, 62, true, false), v.Doca);
        Assert.Equal(new ValoresNotch(FormatoNotch.Encaixado, 34, 1, 16, 10, 0, 16, false), v.Notch);
        Assert.Equal(new ValoresIlhas(DisposicaoIlhas.Separadas, 8, 12, 30, false), v.Ilhas);
        Assert.Equal(46, v.ReservaTopo);
        Assert.Equal(v, R(new FormasPerfil(new FormasDoca(), new FormasNotch(), new FormasIlhas())));
    }

    [Theory]
    [InlineData(TamanhoIcones.Pequeno, 24, 38)]
    [InlineData(TamanhoIcones.Medio, 30, 46)]
    [InlineData(TamanhoIcones.Grande, 38, 56)]
    public void Icones_da_doca(TamanhoIcones tamanho, double icone, double altura)
    {
        var d = R(new FormasPerfil(new FormasDoca(Icones: tamanho))).Doca;
        Assert.Equal(icone, d.Icone);
        Assert.Equal(altura, d.Altura);
        Assert.Equal(Math.Round(icone * 0.8), d.Imagem); // inteiro: ícone fracionário borra (revisão final, minor 4)
        Assert.Equal(icone + 10, d.BotaoLargura);
        Assert.Equal(Math.Min(icone + 14, altura - 2), d.BotaoAltura); // cabe dentro da borda (revisão final, minor 3)
        Assert.Equal(altura + 16, d.Reserva);
    }

    [Fact]
    public void Doca_corrida()
    {
        var d = R(new FormasPerfil(new FormasDoca(FormatoDoca.Corrida, Ampliacao: false))).Doca;
        Assert.Equal(0, d.Folga);
        Assert.Equal(d.Altura, d.Reserva);
        Assert.True(d.CantosRetos);
        Assert.False(d.Ampliacao);
    }

    [Fact]
    public void Notch_por_formato_e_tamanho()
    {
        var sem = R(new FormasPerfil(Notch: new FormasNotch(FormatoNotch.SemOrelhas))).Notch;
        Assert.Equal((0.0, 10.0, 0.0, false), (sem.Orelha, sem.Sobra, sem.Afastamento, sem.CantosDeCima));
        var ilha = R(new FormasPerfil(Notch: new FormasNotch(FormatoNotch.Ilha))).Notch;
        Assert.Equal((0.0, 0.0, 6.0, true), (ilha.Orelha, ilha.Sobra, ilha.Afastamento, ilha.CantosDeCima));
        var compacto = R(new FormasPerfil(Notch: new FormasNotch(Tamanho: TamanhoNotch.Compacto))).Notch;
        Assert.Equal((28.0, 0.85), (compacto.Altura, compacto.EscalaTexto));
        var grande = R(new FormasPerfil(Notch: new FormasNotch(Tamanho: TamanhoNotch.Grande))).Notch;
        Assert.Equal((40.0, 1.15), (grande.Altura, grande.EscalaTexto));
    }

    [Theory]
    [InlineData(FormatoNotch.Encaixado, CantosParte.Redondos, 16)]
    [InlineData(FormatoNotch.Encaixado, CantosParte.Suaves, 10)]
    [InlineData(FormatoNotch.Encaixado, CantosParte.Retos, 4)]
    [InlineData(FormatoNotch.Ilha, CantosParte.Redondos, 17)]
    [InlineData(FormatoNotch.Ilha, CantosParte.Suaves, 10)]
    public void Raio_do_notch_pelos_cantos(FormatoNotch formato, CantosParte cantos, double raio) =>
        Assert.Equal(raio, R(new FormasPerfil(Notch: new FormasNotch(formato)), cantos).Notch.Raio);

    [Fact]
    public void Ilhas_encostadas()
    {
        var i = R(new FormasPerfil(Ilhas: new FormasIlhas(DisposicaoIlhas.BarraUnica, DistanciaIlhas.Encostadas))).Ilhas;
        Assert.Equal(new ValoresIlhas(DisposicaoIlhas.BarraUnica, 0, 0, 30, true), i);
    }

    [Fact]
    public void Reservas_acompanham_as_formas()
    {
        var encostadas = new FormasIlhas(Distancia: DistanciaIlhas.Encostadas);
        Assert.Equal(34, R(new FormasPerfil(Ilhas: encostadas)).ReservaTopo);
        var ilhaGrande = new FormasNotch(FormatoNotch.Ilha, TamanhoNotch.Grande);
        Assert.Equal(46, R(new FormasPerfil(Notch: ilhaGrande, Ilhas: encostadas)).ReservaTopo);
        Assert.Equal(54, R(new FormasPerfil(Notch: ilhaGrande)).ReservaTopo);
        Assert.Equal(72, R(new FormasPerfil(new FormasDoca(Icones: TamanhoIcones.Grande))).Doca.Reserva);
    }
}

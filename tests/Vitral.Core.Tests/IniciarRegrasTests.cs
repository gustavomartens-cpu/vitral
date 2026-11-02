using Vitral.Core.Aparencia;
using Vitral.Core.Iniciar;
using Vitral.Core.Perfis;

namespace Vitral.Core.Tests;

public sealed class IniciarRegrasTests
{
    private static readonly CorArgb Fundo = new(0xCC, 0x0A, 0x0F, 0x20);
    private static readonly CorArgb Borda = new(0x33, 0xFF, 0xFF, 0xFF);

    private static ValoresParte Doca(bool desfoque = true, CantosParte cantos = CantosParte.Redondos) =>
        new(desfoque ? MaterialParte.Vidro : MaterialParte.Solido, Fundo, new CorArgb(255, 255, 255, 255), new CorArgb(255, 200, 200, 200),
            Borda, new CorArgb(255, 0xF2, 0xB5, 0x44), cantos, desfoque);

    [Fact]
    public void Vidro_da_doca_vira_vidro_com_a_tinta_dela() =>
        Assert.Equal(new ValoresIniciar(true, Fundo, Borda, 8), ValoresIniciar.De(Doca()));

    [Fact]
    public void Sem_transparencia_vira_solido()
    {
        var v = ValoresIniciar.De(Doca(desfoque: false));
        Assert.False(v.Vidro);
        Assert.Equal(255, v.Tinta.A);
    }

    [Theory]
    [InlineData(CantosParte.Retos, 0)]
    [InlineData(CantosParte.Suaves, 4)]
    [InlineData(CantosParte.Redondos, 8)]
    public void Cantos_viram_raio(CantosParte cantos, double raio) => Assert.Equal(raio, ValoresIniciar.De(Doca(cantos: cantos)).Raio);

    [Fact]
    public void Json_do_arquivo_de_cores()
    {
        var v = ValoresIniciar.De(Doca());
        Assert.Equal("""{"ligado":true,"vidro":true,"tinta":"#CC0A0F20","borda":"#33FFFFFF","raio":8}""", v.ParaJson(true));
        Assert.Equal("""{"ligado":false}""", v.ParaJson(false));
    }

    [Fact]
    public void Conecta_so_no_modo_Vitral_com_PID_novo_e_sem_Windhawk()
    {
        Assert.True(RegrasIniciar.DeveConectar(ModoIniciar.Vitral, 10, null, false));
        Assert.False(RegrasIniciar.DeveConectar(ModoIniciar.Vitral, 10, 10, false));
        Assert.False(RegrasIniciar.DeveConectar(ModoIniciar.Windows, 10, null, false));
        Assert.False(RegrasIniciar.DeveConectar(ModoIniciar.Vitral, null, null, false));
        Assert.False(RegrasIniciar.DeveConectar(ModoIniciar.Vitral, 10, null, true));
    }

    [Fact]
    public void PID_novo_conecta_de_novo() => Assert.True(RegrasIniciar.DeveConectar(ModoIniciar.Vitral, 11, 10, false));

    [Fact]
    public void Pastas_antigas()
    {
        var pastas = new[] { @"C:\x\iniciar\0.14.0", @"C:\x\iniciar\0.14.1" };
        Assert.Equal([@"C:\x\iniciar\0.14.0"], RegrasIniciar.PastasAntigas(pastas, "0.14.1"));
    }

    [Theory]
    [InlineData(MotivoIniciar.Nenhum, "Iniciar com o visual do Vitral")]
    [InlineData(MotivoIniciar.Windhawk, "Iniciar com o visual do Windows: o Windhawk está usando o Iniciar")]
    [InlineData(MotivoIniciar.Recusou, "Iniciar com o visual do Windows: o Iniciar não aceitou o componente")]
    [InlineData(MotivoIniciar.WindowsMudou, "Iniciar com o visual do Windows: o Windows mudou o Iniciar")]
    [InlineData(MotivoIniciar.SemComponente, "Iniciar com o visual do Windows: o componente não foi encontrado")]
    public void Textos_dos_motivos(MotivoIniciar motivo, string texto) => Assert.Equal(texto, RegrasIniciar.Texto(motivo));
}

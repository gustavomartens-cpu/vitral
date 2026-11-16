using Vitral.Ui.Tema;

namespace Vitral.Ui.Tests;

public sealed class MolaTests
{
    private const double Quadro = 1.0 / 60;

    /// Roda a mola em quadros de 60 Hz até parar (ou até o limite), guardando o maior valor e quanto tempo levou.
    private static (double Maior, double Segundos) Rodar(Mola m, double limite = 3)
    {
        double maior = m.Valor, t = 0;
        while (t < limite && m.Avancar(Quadro))
        {
            t += Quadro;
            maior = Math.Max(maior, m.Valor);
        }
        return (maior, t);
    }

    [Fact]
    public void Abrir_chega_ao_alvo_com_repique_leve()
    {
        var m = new Mola(0);
        m.IrPara(100, JeitoMola.Abrir);
        var (maior, segundos) = Rodar(m);
        Assert.True(m.Parada);
        Assert.Equal(100, m.Valor);
        Assert.InRange(maior, 100.5, 104); // passa um pouco, sem o repique grande
        Assert.InRange(segundos, 0.3, 1.2);
    }

    [Fact]
    public void Fechar_nao_passa_do_alvo()
    {
        var m = new Mola(100);
        m.IrPara(0, JeitoMola.Fechar);
        double menor = 100;
        while (m.Avancar(Quadro)) menor = Math.Min(menor, m.Valor);
        Assert.True(menor >= -Mola.FolgaValor);
        Assert.Equal(0, m.Valor);
    }

    [Fact]
    public void Trocar_o_alvo_no_meio_guarda_a_velocidade()
    {
        var m = new Mola(0);
        m.IrPara(100, JeitoMola.Abrir);
        for (int i = 0; i < 6; i++) m.Avancar(Quadro);
        double velocidade = m.Velocidade, valor = m.Valor;
        Assert.True(velocidade > 0);
        m.IrPara(200, JeitoMola.Abrir);
        Assert.Equal(velocidade, m.Velocidade); // não recomeça parada
        Assert.Equal(valor, m.Valor);
        Rodar(m);
        Assert.Equal(200, m.Valor);
    }

    [Fact]
    public void Quadro_atrasado_e_limitado_a_50_ms()
    {
        var a = new Mola(0);
        var b = new Mola(0);
        a.IrPara(100, JeitoMola.Abrir);
        b.IrPara(100, JeitoMola.Abrir);
        a.Avancar(2); // o computador engasgou dois segundos
        b.Avancar(Mola.QuadroMaximo);
        Assert.Equal(b.Valor, a.Valor, 9);
    }

    [Fact]
    public void Subpassos_nao_dependem_da_taxa_de_quadros()
    {
        var a = new Mola(0);
        var b = new Mola(0);
        a.IrPara(100, JeitoMola.Abrir);
        b.IrPara(100, JeitoMola.Abrir);
        for (int i = 0; i < 10; i++) a.Avancar(0.016);
        for (int i = 0; i < 40; i++) b.Avancar(0.004);
        Assert.Equal(b.Valor, a.Valor, 6);
    }

    [Fact]
    public void Parada_nao_se_move()
    {
        var m = new Mola(42);
        Assert.True(m.Parada);
        Assert.False(m.Avancar(Quadro));
        Assert.Equal(42, m.Valor);
    }

    [Fact]
    public void Pular_vai_direto_e_para()
    {
        var m = new Mola(0);
        m.IrPara(100, JeitoMola.Abrir);
        m.Avancar(Quadro);
        m.Pular(30);
        Assert.Equal((30d, 30d, 0d), (m.Valor, m.Alvo, m.Velocidade));
        Assert.True(m.Parada);
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void Pulso_chega_perto_da_amplitude_e_volta(double amortecimento)
    {
        var m = new Mola(300);
        m.IrPara(300, new JeitoMola(0.42, amortecimento));
        m.Pulsar(20);
        var (maior, segundos) = Rodar(m);
        Assert.InRange(maior, 300 + 20 * 0.85, 300 + 20 * 1.05); // o passo de 4 ms perde uns 10% do pico
        Assert.Equal(300, m.Valor);
        Assert.True(segundos < 1.5);
    }

    [Fact]
    public void Fator_do_pico_vai_a_e_no_amortecimento_critico()
    {
        Assert.Equal(Math.E, Mola.FatorPico(1));
        Assert.Equal(Math.E, Mola.FatorPico(0.9999), 2);
        Assert.Equal(1, Mola.FatorPico(0));
    }
}

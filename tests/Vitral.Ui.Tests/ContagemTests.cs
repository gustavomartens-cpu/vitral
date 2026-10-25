namespace Vitral.Ui.Tests;

public class ContagemTests
{
    [Fact]
    public void Zerar_chama_voltar_uma_vez_so()
    {
        var relogio = new RelogioFalso();
        int voltas = 0, manteve = 0;
        var c = new ContagemViewModel(3, relogio, () => manteve++, () => voltas++);

        relogio.Tique(5);

        Assert.Equal(1, voltas);
        Assert.Equal(0, manteve);
        Assert.Equal(0, c.Restantes);
        Assert.True(relogio.Liberado);
    }

    [Fact]
    public void Manter_antes_de_zerar_nao_volta_e_libera_o_relogio()
    {
        var relogio = new RelogioFalso();
        int voltas = 0, manteve = 0;
        var c = new ContagemViewModel(20, relogio, () => manteve++, () => voltas++);

        relogio.Tique();
        c.ManterCommand.Execute(null);
        relogio.Tique(30);

        Assert.Equal(1, manteve);
        Assert.Equal(0, voltas);
        Assert.True(relogio.Liberado);
    }

    [Fact]
    public void Voltar_manual_conta_uma_vez_mesmo_clicando_de_novo()
    {
        var relogio = new RelogioFalso();
        int voltas = 0;
        var c = new ContagemViewModel(20, relogio, () => { }, () => voltas++);
        c.VoltarCommand.Execute(null);
        c.VoltarCommand.Execute(null);
        relogio.Tique(30);
        Assert.Equal(1, voltas);
    }

    [Fact]
    public void Restantes_desce_a_cada_segundo()
    {
        var relogio = new RelogioFalso();
        var c = new ContagemViewModel(12, relogio, () => { }, () => { });
        Assert.Equal(12, c.Restantes);
        relogio.Tique(2);
        Assert.Equal(10, c.Restantes);
    }
}

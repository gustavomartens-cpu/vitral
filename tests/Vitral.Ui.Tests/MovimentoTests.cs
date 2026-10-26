using Vitral.Ui.Barra;
using Vitral.Ui.Tema;

namespace Vitral.Ui.Tests;

public sealed class MovimentoTests
{
    [Fact]
    public void Lupa_maxima_sob_o_cursor() => Assert.Equal(1.22, Lupa.Escala(0), 6);

    [Fact]
    public void Lupa_meio_alcance() => Assert.Equal(1.11, Lupa.Escala(45), 3);

    [Fact]
    public void Lupa_fora_do_alcance()
    {
        Assert.Equal(1, Lupa.Escala(90));
        Assert.Equal(1, Lupa.Escala(300));
    }

    [Fact]
    public void Lupa_simetrica() => Assert.Equal(Lupa.Escala(30), Lupa.Escala(-30));

    [Fact]
    public void Alvo_relativo_ao_painel_deslocado() =>
        Assert.Equal((30d, 60d), Deslize.Alvo(new Retangulo(130, 5, 60, 24), new Retangulo(100, 0, 300, 34)));

    [Fact]
    public void Cascata_em_passos_de_40_ate_200()
    {
        Assert.Equal([0, 40, 80, 200, 200], new[] { 0, 1, 2, 5, 9 }.Select(i => Cascata.Atraso(i).TotalMilliseconds));
        Assert.Equal(TimeSpan.Zero, Cascata.Atraso(-1));
    }

    [Fact]
    public void Duracao_zero_com_animacao_desligada()
    {
        var d = TimeSpan.FromMilliseconds(180);
        Assert.Equal(TimeSpan.Zero, TempoMovimento.Duracao(d, ligado: false));
        Assert.Equal(d, TempoMovimento.Duracao(d, ligado: true));
    }
}

public sealed class MenuForaTests
{
    [Theory]
    [InlineData(false, true, false, true)]   // apertou fora: fecha
    [InlineData(false, true, true, false)]   // apertou dentro do menu: escolhe o item, não fecha por aqui
    [InlineData(true, true, false, false)]   // já estava apertado (o clique que abriu): espera soltar
    [InlineData(false, false, false, false)] // nada apertado
    public void Fecha_so_num_clique_novo_fora_do_menu(bool antes, bool agora, bool noMenu, bool fecha) =>
        Assert.Equal(fecha, MenuFora.DeveFechar(antes, agora, noMenu));
}

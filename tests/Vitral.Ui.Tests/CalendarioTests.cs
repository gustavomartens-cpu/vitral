using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class CalendarioTests
{
    private readonly AgoraFalso _agora = new(new DateTime(2026, 10, 25, 10, 0, 0));

    [Fact]
    public void Outubro_de_2026_comeca_em_27_de_setembro()
    {
        var vm = new CalendarioViewModel(_agora);
        Assert.Equal(42, vm.Dias.Count);
        Assert.Equal(new DiaCalendario(27, false, false), vm.Dias[0]);
        Assert.Equal(new DiaCalendario(1, true, false), vm.Dias[4]);
        Assert.Equal(25, vm.Dias.Single(d => d.Hoje).Numero);
    }

    [Fact]
    public void Titulo_e_data_por_extenso()
    {
        var vm = new CalendarioViewModel(_agora);
        Assert.Equal("outubro de 2026", vm.Titulo);
        Assert.Equal("domingo, 25 de outubro de 2026", vm.DataPorExtenso);
    }

    [Fact]
    public void Navega_e_atravessa_o_ano()
    {
        var vm = new CalendarioViewModel(_agora);
        vm.ProximoCommand.Execute(null);
        vm.ProximoCommand.Execute(null);
        vm.ProximoCommand.Execute(null);
        Assert.Equal("janeiro de 2027", vm.Titulo);
        Assert.DoesNotContain(vm.Dias, d => d.Hoje);
        vm.AnteriorCommand.Execute(null);
        Assert.Equal("dezembro de 2026", vm.Titulo);
    }

    [Fact]
    public void Hoje_volta_ao_mes_atual()
    {
        var vm = new CalendarioViewModel(_agora);
        vm.AnteriorCommand.Execute(null);
        vm.HojeCommand.Execute(null);
        Assert.Equal("outubro de 2026", vm.Titulo);
    }

    [Fact]
    public void Reabrir_rele_o_dia_de_hoje()
    {
        var vm = new CalendarioViewModel(_agora);
        vm.ProximoCommand.Execute(null);
        _agora.Agora = new DateTime(2026, 11, 2);
        vm.Reabrir();
        Assert.Equal("novembro de 2026", vm.Titulo);
        Assert.Equal(2, vm.Dias.Single(d => d.Hoje).Numero);
    }
}

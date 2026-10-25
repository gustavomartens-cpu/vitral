using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PainelBateriaTests
{
    private readonly BateriaFalsa _bateria = new() { Atual = new EstadoBateria(81, false, new TimeSpan(3, 20, 0)) };
    private readonly ModoEnergiaFalso _modo = new();

    private PainelBateriaViewModel Criar() => new(_bateria, _modo);

    [Theory]
    [InlineData(3, 20, "3 h 20 min")]
    [InlineData(0, 45, "45 min")]
    [InlineData(3, 0, "3 h")]
    public void Tempo_restante_formatado(int horas, int minutos, string esperado)
    {
        _bateria.Atual = new EstadoBateria(81, false, new TimeSpan(horas, minutos, 0));
        using var vm = Criar();
        Assert.Equal(esperado, vm.Detalhe);
        Assert.Equal("81%", vm.Porcentagem);
        Assert.Equal(0.81, vm.Fracao, 3);
    }

    [Fact]
    public void Carregando_e_conectado()
    {
        _bateria.Atual = new EstadoBateria(60, true);
        using var vm = Criar();
        Assert.Equal("Carregando", vm.Detalhe);
        _bateria.Atual = new EstadoBateria(100, true);
        _bateria.Disparar();
        Assert.Equal("Conectado", vm.Detalhe);
    }

    [Fact]
    public void Tempo_desconhecido_mostra_usando_a_bateria()
    {
        _bateria.Atual = new EstadoBateria(50, false);
        using var vm = Criar();
        Assert.Equal("Usando a bateria", vm.Detalhe);
    }

    [Fact]
    public void Modo_atual_marcado_e_definir_chega_na_fonte()
    {
        using var vm = Criar();
        Assert.Equal(ModoEnergia.Equilibrado, vm.Modo);
        Assert.True(vm.MostrarModo);
        vm.Modo = ModoEnergia.Desempenho;
        Assert.Equal([ModoEnergia.Desempenho], _modo.Definidos);
    }

    [Fact]
    public void Aviso_da_fonte_nao_volta_como_definir()
    {
        using var vm = Criar();
        _modo.Atual = ModoEnergia.Economia;
        _modo.Definir(ModoEnergia.Economia); // simula aviso; o único Definir é este
        Assert.Equal(ModoEnergia.Economia, vm.Modo);
        Assert.Single(_modo.Definidos);
    }

    [Fact]
    public void Modo_indisponivel_esconde_o_seletor()
    {
        _modo.Atual = null;
        using var vm = Criar();
        Assert.False(vm.MostrarModo);
    }

    [Fact]
    public void Abrir_rele_o_modo()
    {
        using var vm = Criar();
        vm.Abrir();
        Assert.Equal(1, _modo.Relidas);
    }
}

public sealed class OpcoesEnergiaTests
{
    [Fact]
    public void Modos_de_energia_na_ordem_do_mockup()
    {
        Assert.Equal(["Economia", "Equilibrado", "Desempenho"], Opcoes.ModosEnergia.Select(o => o.Rotulo));
        Assert.Equal([ModoEnergia.Economia, ModoEnergia.Equilibrado, ModoEnergia.Desempenho], Opcoes.ModosEnergia.Select(o => o.Valor));
    }
}

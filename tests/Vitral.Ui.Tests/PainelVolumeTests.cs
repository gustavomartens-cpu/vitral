using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PainelVolumeTests
{
    private readonly VolumeFalso _volume = new();
    private readonly DispositivosFalsos _dispositivos = new();

    private PainelVolumeViewModel Criar() => new(_volume, _dispositivos);

    [Fact]
    public void Mover_o_controle_define_o_volume()
    {
        using var vm = Criar();
        vm.Volume = 62;
        Assert.Equal([62], _volume.Definidos);
    }

    [Fact]
    public void Aviso_da_fonte_nao_volta_como_definir()
    {
        using var vm = Criar();
        _volume.Volume = 40;
        _volume.Disparar();
        Assert.Equal(40, vm.Volume);
        Assert.Empty(_volume.Definidos);
    }

    [Fact]
    public void Lista_com_o_padrao_marcado()
    {
        using var vm = Criar();
        Assert.Equal(["Alto-falantes", "Fone Bluetooth"], vm.Saidas.Select(s => s.Nome));
        Assert.True(vm.Saidas[0].Padrao);
        Assert.True(vm.MostrarSaidas);
    }

    [Fact]
    public void Escolher_troca_o_padrao()
    {
        using var vm = Criar();
        vm.EscolherCommand.Execute(vm.Saidas[1]);
        Assert.Equal(["fone"], _dispositivos.Tornados);
    }

    [Fact]
    public void Troca_indisponivel_esconde_a_lista()
    {
        _dispositivos.PodeTrocar = false;
        using var vm = Criar();
        Assert.False(vm.MostrarSaidas);
    }

    [Fact]
    public void Volume_indisponivel()
    {
        _volume.Volume = null;
        using var vm = Criar();
        Assert.False(vm.Disponivel);
    }

    [Fact]
    public void Mudo_pelo_painel()
    {
        using var vm = Criar();
        vm.AlternarMudoCommand.Execute(null);
        Assert.Equal(1, _volume.MudoAlternado);
        Assert.True(vm.Mudo);
    }
}

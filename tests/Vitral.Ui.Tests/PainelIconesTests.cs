using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PainelIconesTests
{
    private readonly BandejaFalsa _bandeja = new();

    [Fact]
    public void Icones_que_o_topo_ja_mostra_ficam_de_fora()
    {
        using var vm = new PainelIconesViewModel(_bandeja);
        Assert.Equal(["Wallpaper Engine", "Steam"], vm.Icones.Select(i => i.Titulo));
    }

    [Fact]
    public void Cliques_chegam_na_fonte()
    {
        using var vm = new PainelIconesViewModel(_bandeja);
        vm.ClicarEsquerdoCommand.Execute(vm.Icones[0]);
        vm.ClicarDireitoCommand.Execute(vm.Icones[1]);
        Assert.Equal([("wallpaper", BotaoMouse.Esquerdo), ("steam", BotaoMouse.Direito)], _bandeja.Cliques);
    }

    [Fact]
    public void Direito_no_icone_do_Vitral_pede_o_menu_dele()
    {
        // O menu do próprio Vitral abria dentro do clique repassado e fechava na hora (Gustavo, 16/11/2026): o painel pede
        // o menu ao App em vez de repassar.
        _bandeja.Icones = [new("vitral", "Vitral", null, false) { DoVitral = true }];
        using var vm = new PainelIconesViewModel(_bandeja);
        int pedidos = 0;
        vm.PediuMenuDoVitral += () => pedidos++;
        vm.ClicarDireitoCommand.Execute(vm.Icones[0]);
        Assert.Equal(1, pedidos);
        Assert.Empty(_bandeja.Cliques);
        vm.ClicarEsquerdoCommand.Execute(vm.Icones[0]); // o esquerdo continua indo (abre a janela)
        Assert.Equal([("vitral", BotaoMouse.Esquerdo)], _bandeja.Cliques);
    }

    [Fact]
    public void Lista_acompanha_a_fonte()
    {
        using var vm = new PainelIconesViewModel(_bandeja);
        _bandeja.Icones = [new("discord", "Discord", null, false)];
        _bandeja.Disparar();
        Assert.Equal(["Discord"], vm.Icones.Select(i => i.Titulo));
    }

    [Fact]
    public void Indisponivel_esconde_a_setinha()
    {
        _bandeja.Disponivel = false;
        using var vm = new PainelIconesViewModel(_bandeja);
        Assert.False(vm.Disponivel);
    }
}

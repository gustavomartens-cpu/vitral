using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class MenuBandejaTests : IDisposable
{
    private readonly Ambiente _amb = new();
    public void Dispose() => _amb.Dispose();

    [Fact]
    public void Menu_tem_perfis_marcados_e_acoes_na_ordem()
    {
        var itens = MenuBandeja.Montar(["A", "B"], "B");

        Assert.Equal(
            ["A", "B", "", "Desfazer última mudança", "Voltar ao Windows original", "", "Abrir Vitral", "Sair"],
            itens.Select(i => i.Texto));
        Assert.Equal(["perfil:A", "perfil:B", "", "desfazer", "original", "", "abrir", "sair"], itens.Select(i => i.Acao));
        Assert.False(itens[0].Marcado);
        Assert.True(itens[1].Marcado);
        Assert.True(itens[2].Separador);
        Assert.True(itens[5].Separador);
    }

    [Fact]
    public void Sem_perfis_o_menu_so_tem_as_acoes()
    {
        var itens = MenuBandeja.Montar([], null);
        Assert.Equal("desfazer", itens[0].Acao);
    }

    [Fact]
    public void Troca_pela_bandeja_usa_contagem_de_12_segundos_e_volta_sem_resposta()
    {
        _amb.Perfis.Salvar(new Perfil(1, "Claro", null, new AjustesNativosPerfil(null, Modo.Claro, null, null, null, null, null, null)));
        var relogio = new RelogioFalso();
        var vm = new PrincipalViewModel(_amb.Servico, _amb.Perfis, new DialogosFalsos(), relogio);

        vm.TrocarPelaBandeja("Claro");

        Assert.Equal(12, vm.Contagem!.Restantes);
        Assert.Equal(1u, _amb.Apps());
        relogio.Tique(12);
        Assert.Equal(0u, _amb.Apps());
    }
}

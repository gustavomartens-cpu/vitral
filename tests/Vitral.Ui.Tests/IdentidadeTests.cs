using System.Collections.Specialized;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class IdentidadeTests : IDisposable
{
    private readonly Ambiente _amb = new();
    private readonly DialogosFalsos _dialogos = new();
    private readonly RelogioFalso _relogio = new();

    public void Dispose() => _amb.Dispose();

    private static Perfil Novo(string nome, string? cor = null, Modo apps = Modo.Claro) =>
        new(1, nome, null, new AjustesNativosPerfil(null, apps, cor, null, null, null, null, null));

    private PrincipalViewModel Criar() => new(_amb.Servico, _amb.Perfis, _dialogos, _relogio);

    [Fact]
    public void Item_usa_a_cor_do_perfil_ou_o_azul_noite()
    {
        _amb.Perfis.Salvar(Novo("Vermelho", "#E81123"));
        _amb.Perfis.Salvar(Novo("Sem cor"));
        var vm = Criar();
        Assert.Contains(new ItemPerfil("Vermelho", "#E81123", false), vm.ItensPerfis);
        Assert.Contains(new ItemPerfil("Sem cor", "#4C7DF0", false), vm.ItensPerfis);
    }

    [Fact]
    public void Ativo_acompanha_manter_e_aplicar()
    {
        _amb.Perfis.Salvar(Novo("A"));
        _amb.Perfis.Salvar(Novo("B", apps: Modo.Escuro));
        var vm = Criar();

        vm.Selecionado = "A";
        vm.TestarCommand.Execute(null);
        vm.Contagem!.ManterCommand.Execute(null);
        Assert.Equal(["A"], vm.ItensPerfis.Where(i => i.Ativo).Select(i => i.Nome));

        vm.Selecionado = "B";
        vm.AplicarCommand.Execute(null);
        Assert.Equal(["B"], vm.ItensPerfis.Where(i => i.Ativo).Select(i => i.Nome));
    }

    [Fact]
    public void Sincronia_dos_itens_nao_usa_reset()
    {
        _amb.Perfis.Salvar(Novo("A"));
        var vm = Criar();
        var acoes = new List<NotifyCollectionChangedAction>();
        vm.ItensPerfis.CollectionChanged += (_, e) => acoes.Add(e.Action);

        _amb.Perfis.Salvar(Novo("B"));
        vm.AtualizarLista();
        vm.Selecionado = "A";
        vm.AplicarCommand.Execute(null);

        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, acoes);
        Assert.Equal(["A", "B"], vm.ItensPerfis.Select(i => i.Nome));
    }

    [Fact]
    public void Salvar_cor_nova_atualiza_a_gema()
    {
        _amb.Perfis.Salvar(Novo("A"));
        var vm = Criar();
        vm.Selecionado = "A";
        vm.Editor.CorDestaque = "#107C10";
        vm.SalvarPerfilCommand.Execute(null);
        Assert.Equal("#107C10", vm.ItensPerfis.Single().CorGema);
    }

    [Fact]
    public void Descartar_volta_ao_que_esta_salvo()
    {
        _amb.Perfis.Salvar(Novo("A"));
        var vm = Criar();
        vm.Selecionado = "A";
        vm.Editor.Transparencia = Escolha.Nao;

        vm.DescartarCommand.Execute(null);

        Assert.Equal(Escolha.NaoMexer, vm.Editor.Transparencia);
        Assert.Equal(ModoOpcional.Claro, vm.Editor.ModoApps);
    }

    [Fact]
    public void Descartar_sem_selecao_avisa()
    {
        var vm = Criar();
        vm.DescartarCommand.Execute(null);
        Assert.Single(_dialogos.Avisos);
    }

    [Fact]
    public void Descartar_perfil_apagado_avisa_sem_lancar()
    {
        _amb.Perfis.Salvar(Novo("A"));
        var vm = Criar();
        vm.Selecionado = "A";
        Directory.Delete(Path.Combine(_amb.Perfis.Pasta, "A"), recursive: true);

        vm.DescartarCommand.Execute(null);

        Assert.Single(_dialogos.Avisos);
    }

    [Fact]
    public void Opcoes_dos_seletores_na_ordem_certa()
    {
        Assert.Equal(["Não mexer", "Claro", "Escuro"], Opcoes.Modos.Select(o => o.Rotulo));
        Assert.Equal([ModoOpcional.NaoMexer, ModoOpcional.Claro, ModoOpcional.Escuro], Opcoes.Modos.Select(o => o.Valor));
        Assert.Equal(["Não mexer", "Ligada", "Desligada"], Opcoes.Ligada.Select(o => o.Rotulo));
        Assert.Equal([Escolha.NaoMexer, Escolha.Sim, Escolha.Nao], Opcoes.Ligada.Select(o => o.Valor));
        Assert.Equal(["Não mexer", "Centro", "Esquerda"], Opcoes.Alinhamentos.Select(o => o.Rotulo));
    }
}

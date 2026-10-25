using Vitral.Core.Nativo;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class EditorBarraTarefasTests : IDisposable
{
    private readonly Ambiente _amb = new();
    private readonly List<ProgramaBandeja> _programas = [new("Spotify.exe", "Spotify"), new("steam.exe", "Steam")];

    public void Dispose() => _amb.Dispose();

    private EditorBarraTarefasViewModel Editor() => new(() => _programas);

    private static readonly BarraTarefasPerfil Completa = new(PesquisaBarra.Caixa, true, false, AgruparBotoes.Nunca, true, false, true, false,
        ["steam.exe"]);

    [Fact]
    public void Carregar_e_devolver_ida_e_volta()
    {
        var e = Editor();
        e.Carregar(Completa);
        Assert.Equal(Completa, e.ParaPerfil());
        e.Carregar(null);
        Assert.Null(e.ParaPerfil());
    }

    [Fact]
    public void Tudo_nao_mexer_vira_nulo()
    {
        var e = Editor();
        e.Carregar(new BarraTarefasPerfil(Selos: true));
        e.Selos = Escolha.NaoMexer;
        Assert.Null(e.ParaPerfil());
    }

    [Fact]
    public void Escolher_icones_lista_os_programas_marcando_os_do_perfil()
    {
        var e = Editor();
        e.Carregar(new BarraTarefasPerfil(IconesVisiveis: ["STEAM.EXE"]));
        Assert.Equal(Escolha.Sim, e.EscolherIcones);
        Assert.Equal(["Spotify", "Steam"], e.Icones.Select(i => i.Nome));
        Assert.False(e.Icones[0].Visivel);
        Assert.True(e.Icones[1].Visivel);
    }

    [Fact]
    public void Programa_do_perfil_que_nao_existe_continua_na_lista()
    {
        var e = Editor();
        e.Carregar(new BarraTarefasPerfil(IconesVisiveis: ["Notebook.exe"]));
        var ausente = e.Icones.Single(i => i.Executavel == "Notebook.exe");
        Assert.Equal("Notebook (não encontrado aqui)", ausente.Nome);
        Assert.True(ausente.Visivel);
        Assert.Equal(["Notebook.exe"], e.ParaPerfil()!.IconesVisiveis!);
    }

    [Fact]
    public void Nao_mexer_nos_icones_vira_lista_nula()
    {
        var e = Editor();
        e.Carregar(new BarraTarefasPerfil(Selos: true, IconesVisiveis: ["steam.exe"]));
        e.EscolherIcones = Escolha.NaoMexer;
        Assert.Null(e.ParaPerfil()!.IconesVisiveis);
    }

    [Fact]
    public void Salvar_grava_a_secao_no_perfil()
    {
        _amb.Perfis.Salvar(new Perfil(3, "A", null, null));
        var vm = new PrincipalViewModel(_amb.Servico, _amb.Perfis, new DialogosFalsos(), new RelogioFalso(), () => _programas);
        vm.Selecionado = "A";
        vm.EditorBarraTarefas.Pesquisa = PesquisaOpcional.Oculta;
        vm.EditorBarraTarefas.EscolherIcones = Escolha.Sim;
        vm.EditorBarraTarefas.Icones[0].Visivel = true;

        vm.SalvarPerfilCommand.Execute(null);

        var p = _amb.Perfis.Carregar("A");
        Assert.Equal(new BarraTarefasPerfil(PesquisaBarra.Oculta, IconesVisiveis: ["Spotify.exe"]), p.BarraTarefas);
        Assert.Contains("\"esquema\": 4", File.ReadAllText(Path.Combine(_amb.Perfis.Pasta, "A", "perfil.json")));
    }

    [Fact]
    public void Descartar_volta_a_secao_salva()
    {
        _amb.Perfis.Salvar(new Perfil(3, "A", null, null, null, new BarraTarefasPerfil(Agrupar: AgruparBotoes.Sempre)));
        var vm = new PrincipalViewModel(_amb.Servico, _amb.Perfis, new DialogosFalsos(), new RelogioFalso(), () => _programas);
        vm.Selecionado = "A";
        vm.EditorBarraTarefas.Agrupar = AgruparOpcional.Nunca;
        vm.DescartarCommand.Execute(null);
        Assert.Equal(AgruparOpcional.Sempre, vm.EditorBarraTarefas.Agrupar);
    }

    [Fact]
    public void Opcoes_dos_seletores()
    {
        Assert.Equal(["Não mexer", "Oculta", "Ícone", "Caixa"], Opcoes.Pesquisas.Select(o => o.Rotulo));
        Assert.Equal(["Não mexer", "Sempre", "Quando cheia", "Nunca"], Opcoes.Agrupamentos.Select(o => o.Rotulo));
    }
}

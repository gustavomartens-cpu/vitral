using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class EditorBarraTests : IDisposable
{
    private readonly Ambiente _amb = new();
    private readonly DialogosFalsos _dialogos = new();

    public void Dispose() => _amb.Dispose();

    private PrincipalViewModel Criar() => new(_amb.Servico, _amb.Perfis, _dialogos, new RelogioFalso());

    private void Salvar(string nome, BarraTopoPerfil? barra, Modo? apps = null) =>
        _amb.Perfis.Salvar(new Perfil(2, nome, null, new AjustesNativosPerfil(null, apps, null, null, null, null, null, null), barra));

    public static TheoryData<BarraTopoPerfil?> Estados => new()
    {
        null,
        BarraTopoPerfil.Desligada,
        new BarraTopoPerfil(true, new IlhasBarra(Desempenho: false)),
    };

    [Theory]
    [MemberData(nameof(Estados))]
    public void Carregar_e_devolver_ida_e_volta(BarraTopoPerfil? estado)
    {
        var e = new EditorBarraViewModel();
        e.Carregar(estado);
        var volta = e.ParaPerfil();
        if (estado is { Visivel: true }) Assert.Equal(estado.IlhasEfetivas, volta!.IlhasEfetivas);
        Assert.Equal(estado?.Visivel, volta?.Visivel);
    }

    [Fact]
    public void Ilhas_so_habilitadas_com_barra_ligada()
    {
        var e = new EditorBarraViewModel();
        Assert.False(e.IlhasHabilitadas);
        e.Barra = Escolha.Sim;
        Assert.True(e.IlhasHabilitadas);
        e.Barra = Escolha.Nao;
        Assert.False(e.IlhasHabilitadas);
    }

    [Fact]
    public void Salvar_grava_barra_no_perfil()
    {
        Salvar("A", null, Modo.Claro);
        var vm = Criar();
        vm.Selecionado = "A";
        vm.EditorBarra.Barra = Escolha.Sim;
        vm.EditorBarra.Desempenho = false;

        vm.SalvarPerfilCommand.Execute(null);

        var p = _amb.Perfis.Carregar("A");
        Assert.Equal(new BarraTopoPerfil(true, new IlhasBarra(Desempenho: false)), p.BarraTopo);
        Assert.Equal(Modo.Claro, p.Nativo!.ModoApps);
        Assert.Contains("\"esquema\": 2", File.ReadAllText(Path.Combine(_amb.Perfis.Pasta, "A", "perfil.json")));
    }

    [Fact]
    public void Descartar_volta_a_barra_salva()
    {
        Salvar("A", BarraTopoPerfil.Desligada);
        var vm = Criar();
        vm.Selecionado = "A";
        vm.EditorBarra.Barra = Escolha.Sim;

        vm.DescartarCommand.Execute(null);

        Assert.Equal(Escolha.Nao, vm.EditorBarra.Barra);
    }

    [Fact]
    public void Trocar_de_perfil_carrega_a_barra()
    {
        Salvar("A", new BarraTopoPerfil(true, new IlhasBarra(Midia: false)));
        Salvar("B", null);
        var vm = Criar();
        vm.Selecionado = "A";
        Assert.Equal(Escolha.Sim, vm.EditorBarra.Barra);
        Assert.False(vm.EditorBarra.Midia);
        vm.Selecionado = "B";
        Assert.Equal(Escolha.NaoMexer, vm.EditorBarra.Barra);
        Assert.True(vm.EditorBarra.Midia);
    }
}

using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class EditorDocaTests : IDisposable
{
    private readonly Ambiente _amb = new();
    private readonly DialogosFalsos _dialogos = new();

    public void Dispose() => _amb.Dispose();

    private PrincipalViewModel Criar() => new(_amb.Servico, _amb.Perfis, _dialogos, new RelogioFalso());

    private void Salvar(string nome, BarraBaixoPerfil? doca) =>
        _amb.Perfis.Salvar(new Perfil(4, nome, null, new AjustesNativosPerfil(null, null, null, null, null, null, null, null), BarraBaixo: doca));

    public static TheoryData<BarraBaixoPerfil?> Estados => new() { null, new BarraBaixoPerfil(true), BarraBaixoPerfil.Desligada };

    [Theory]
    [MemberData(nameof(Estados))]
    public void Carregar_e_devolver_ida_e_volta(BarraBaixoPerfil? estado)
    {
        var e = new EditorDocaViewModel();
        e.Carregar(estado);
        Assert.Equal(estado, e.ParaPerfil());
    }

    [Fact]
    public void Salvar_grava_a_doca_no_perfil()
    {
        Salvar("A", null);
        var vm = Criar();
        vm.Selecionado = "A";
        vm.EditorDoca.Doca = Escolha.Sim;

        vm.SalvarPerfilCommand.Execute(null);

        Assert.Equal(new BarraBaixoPerfil(true), _amb.Perfis.Carregar("A").BarraBaixo);
        var json = File.ReadAllText(Path.Combine(_amb.Perfis.Pasta, "A", "perfil.json"));
        Assert.Contains("\"esquema\": 4", json);
        Assert.Contains("\"barraBaixo\"", json);
    }

    [Fact]
    public void Descartar_volta_a_doca_salva()
    {
        Salvar("A", BarraBaixoPerfil.Desligada);
        var vm = Criar();
        vm.Selecionado = "A";
        vm.EditorDoca.Doca = Escolha.Sim;

        vm.DescartarCommand.Execute(null);

        Assert.Equal(Escolha.Nao, vm.EditorDoca.Doca);
    }

    // Repaginação: com a doca ligada no perfil, as opções da barra do Windows não têm onde aparecer e ficam recolhidas.
    [Theory]
    [InlineData(Escolha.Sim, true)]
    [InlineData(Escolha.Nao, false)]
    [InlineData(Escolha.NaoMexer, false)]
    public void Doca_ligada_substitui_a_barra_do_Windows(Escolha doca, bool substitui)
    {
        var e = new EditorDocaViewModel { Doca = doca };
        Assert.Equal(substitui, e.SubstituiBarraDoWindows);
    }

    [Fact]
    public void Substitui_avisa_quando_a_doca_muda()
    {
        var e = new EditorDocaViewModel();
        var mudou = new List<string?>();
        e.PropertyChanged += (_, a) => mudou.Add(a.PropertyName);
        e.Doca = Escolha.Sim;
        Assert.Contains(nameof(EditorDocaViewModel.SubstituiBarraDoWindows), mudou);
    }
}

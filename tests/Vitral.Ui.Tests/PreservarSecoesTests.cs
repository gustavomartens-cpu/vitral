using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class PreservarSecoesTests : IDisposable
{
    private readonly Ambiente _amb = new();

    public void Dispose() => _amb.Dispose();

    [Fact]
    public void Salvar_preserva_secoes_que_o_editor_nao_edita()
    {
        _amb.Perfis.Salvar(new Perfil(4, "D", null, null, BarraBaixo: new BarraBaixoPerfil(true)));
        var vm = new PrincipalViewModel(_amb.Servico, _amb.Perfis, new DialogosFalsos(), new RelogioFalso());
        vm.Selecionado = "D";
        vm.Editor.ModoApps = ModoOpcional.Escuro;

        vm.SalvarPerfilCommand.Execute(null);

        var p = _amb.Perfis.Carregar("D");
        Assert.Equal(new BarraBaixoPerfil(true), p.BarraBaixo);
        Assert.Equal(Modo.Escuro, p.Nativo!.ModoApps);
    }
}

using Vitral.Core.Iniciar;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

public sealed class EditorIniciarTests : IDisposable
{
    private readonly Ambiente _amb = new();
    public void Dispose() => _amb.Dispose();

    [Fact]
    public void Ida_e_volta_do_perfil()
    {
        var e = new EditorIniciarViewModel();
        e.Carregar(new IniciarPerfil(ModoIniciar.Vitral));
        Assert.Equal(new IniciarPerfil(ModoIniciar.Vitral), e.ParaPerfil());
        e.Carregar(null);
        Assert.Null(e.ParaPerfil());
    }

    [Fact]
    public void Mudar_o_modo_marca_alteracao_e_salvar_grava()
    {
        _amb.Perfis.Salvar(new Perfil(4, "A", null, null));
        var vm = new PrincipalViewModel(_amb.Servico, _amb.Perfis, new DialogosFalsos(), new RelogioFalso());
        vm.Selecionado = "A";
        vm.EditorIniciar.Modo = ModoIniciar.Vitral;
        Assert.True(vm.TemAlteracoes);
        vm.SalvarPerfilCommand.Execute(null);
        Assert.Equal(ModoIniciar.Vitral, _amb.Perfis.Carregar("A").Iniciar?.Modo);
        Assert.False(vm.TemAlteracoes);
    }

    [Fact]
    public void Estado_vira_texto_e_reserva()
    {
        var e = new EditorIniciarViewModel { Estado = MotivoIniciar.Windhawk };
        Assert.Equal("Iniciar com o visual do Windows: o Windhawk está usando o Iniciar", e.TextoEstado);
        Assert.True(e.MostrarReserva);
        e.Estado = MotivoIniciar.Nenhum;
        Assert.Equal("Iniciar com o visual do Vitral", e.TextoEstado);
        Assert.False(e.MostrarReserva);
        e.Estado = null; // modo Windows: a linha some
        Assert.Null(e.TextoEstado);
        Assert.False(e.MostrarReserva);
    }

    [Fact]
    public void Botao_pede_o_destaque()
    {
        var e = new EditorIniciarViewModel();
        int pedidos = 0;
        e.PediuDestaqueDoWindows += () => pedidos++;
        e.UsarDestaqueCommand.Execute(null);
        Assert.Equal(1, pedidos);
    }
}

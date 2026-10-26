using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class RegistroAppBarTests
{
    /// O Explorer visto pela reserva: lembra se a janela está registrada e recusa um segundo ABM_NEW.
    private sealed class ExplorerFalso : IAppBar
    {
        public bool Conhece { get; private set; }
        public int Posicionamentos { get; private set; }

        public bool Novo()
        {
            if (Conhece) return false;
            Conhece = true;
            return true;
        }

        public void Remover() => Conhece = false;

        public void Posicionar()
        {
            if (Conhece) Posicionamentos++;
        }

        public void Reiniciar() => Conhece = false;
    }

    [Fact]
    public void Registra_e_posiciona()
    {
        var explorer = new ExplorerFalso();
        var registro = new RegistroAppBar(explorer);
        Assert.True(registro.Registrar());
        Assert.True(explorer.Conhece);
        Assert.Equal(1, explorer.Posicionamentos);
    }

    [Fact]
    public void TaskbarCreated_da_propria_bandeja_com_a_reserva_viva_registra_e_reposiciona()
    {
        // A bandeja do Vitral (ManagedShell) difunde "TaskbarCreated" ao ligar, sem o Explorer ter reiniciado.
        var explorer = new ExplorerFalso();
        var registro = new RegistroAppBar(explorer);
        registro.Registrar();

        registro.AoCriarBarra();
        Assert.True(registro.Registrar());
        Assert.True(registro.Registrada);

        // Depois o ocultar automático do Windows pega e chega o ABN_POSCHANGED: a reserva desce para a borda.
        registro.Reposicionar();
        Assert.Equal(3, explorer.Posicionamentos);
    }

    [Fact]
    public void TaskbarCreated_do_Explorer_reiniciado_registra_de_novo()
    {
        var explorer = new ExplorerFalso();
        var registro = new RegistroAppBar(explorer);
        registro.Registrar();

        explorer.Reiniciar();
        registro.AoCriarBarra();
        Assert.True(registro.Registrar());
        Assert.True(explorer.Conhece);
        Assert.Equal(2, explorer.Posicionamentos);
    }

    [Fact]
    public void Liberar_remove_e_nao_posiciona_mais()
    {
        var explorer = new ExplorerFalso();
        var registro = new RegistroAppBar(explorer);
        registro.Registrar();
        registro.Liberar();
        registro.Reposicionar();
        Assert.False(explorer.Conhece);
        Assert.Equal(1, explorer.Posicionamentos);
    }
}

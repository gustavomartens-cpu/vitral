using Vitral.Core.Notificacoes;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PainelNotificacoesTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(-3));
    private static NotificacaoApp N(uint id, string app, double min) => new(id, app, app + " (nome)", $"T{id}", "texto", Agora.AddMinutes(-min));
    private static PainelNotificacoesViewModel Criar() => new(() => Agora);

    [Fact]
    public void Agrupa_e_mostra_o_estado()
    {
        var p = Criar();
        p.Atualizar(EstadoNotificacoes.Ligado, [N(1, "Chrome", 5), N(2, "Email", 1), N(3, "Chrome", 30)]);
        Assert.Equal(["Email", "Chrome"], p.Grupos.Select(g => g.AppId));
        Assert.Equal("Chrome (nome)", p.Grupos[1].NomeApp);
        Assert.Equal(["há 5 min", "há 30 min"], p.Grupos[1].Itens.Select(i => i.Quando));
        Assert.Null(p.TextoEstado);
        Assert.False(p.Vazio);
        p.Atualizar(EstadoNotificacoes.SemPermissao, []);
        Assert.Equal(RegrasNotificacoes.Texto(EstadoNotificacoes.SemPermissao), p.TextoEstado);
        Assert.False(p.Vazio); // o texto do estado ocupa o lugar do "Nenhuma notificação"
    }

    [Fact]
    public void Limpar_um_grupo_e_tudo_pedem_ao_servico()
    {
        var p = Criar();
        p.Atualizar(EstadoNotificacoes.Ligado, [N(1, "Chrome", 5)]);
        var pedidos = new List<string>();
        p.PediuLimpar += id => pedidos.Add($"um {id}");
        p.PediuLimparApp += app => pedidos.Add($"app {app}");
        p.PediuLimparTudo += () => pedidos.Add("tudo");
        p.PediuAbrirApp += app => pedidos.Add($"abrir {app}");
        p.PediuCentral += () => pedidos.Add("central");
        p.LimparCommand.Execute(p.Grupos[0].Itens[0]);
        p.LimparAppCommand.Execute(p.Grupos[0]);
        p.LimparTudoCommand.Execute(null);
        p.AbrirAppCommand.Execute(p.Grupos[0].Itens[0]);
        p.AbrirCentralCommand.Execute(null);
        Assert.Equal(["um 1", "app Chrome", "tudo", "abrir Chrome", "central"], pedidos);
    }

    [Fact]
    public void Vazio_mostra_nenhuma()
    {
        var p = Criar();
        p.Atualizar(EstadoNotificacoes.Ligado, []);
        Assert.True(p.Vazio);
        Assert.Empty(p.Grupos);
    }
}

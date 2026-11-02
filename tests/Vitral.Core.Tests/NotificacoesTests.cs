using System.Diagnostics;
using Vitral.Core.Notificacoes;

namespace Vitral.Core.Tests;

public sealed class NotificacoesTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(-3));

    private static NotificacaoApp N(uint id, string app, double minutosAtras, string nome = "") =>
        new(id, app, nome.Length > 0 ? nome : app, "Título", "Texto", Agora.AddMinutes(-minutosAtras));

    [Fact]
    public void Filtra_o_app_do_Claude()
    {
        var lista = RegrasNotificacoes.Filtrar([N(1, "Chrome", 1), N(2, "Claude_pzs8sxrjxfjjc!Claude", 1)]);
        Assert.Equal([1u], lista.Select(n => n.Id));
    }

    [Fact]
    public void Agrupa_por_app_com_o_mais_recente_em_cima()
    {
        var grupos = RegrasNotificacoes.Agrupar([N(1, "Chrome", 30), N(2, "Email", 5), N(3, "Chrome", 2), N(4, "Email", 50)]);
        Assert.Equal(["Chrome", "Email"], grupos.Select(g => g.AppId));
        Assert.Equal([3u, 1u], grupos[0].Itens.Select(n => n.Id));
        Assert.Equal([2u, 4u], grupos[1].Itens.Select(n => n.Id));
    }

    [Fact]
    public void Diferenca_entre_leituras()
    {
        NotificacaoApp[] antes = [N(1, "Chrome", 10), N(2, "Email", 5)];
        NotificacaoApp[] agora = [N(2, "Email", 5), N(3, "Xbox", 0)];
        var (novas, sumidas) = RegrasNotificacoes.Diferenca(antes, agora);
        Assert.Equal([3u], novas.Select(n => n.Id));
        Assert.Equal([1u], sumidas);
    }

    [Theory]
    [InlineData(0.5, "agora")]
    [InlineData(5, "há 5 min")]
    [InlineData(180, "há 3 h")]
    [InlineData(2 * 24 * 60, "31/10 10:00")]
    public void Ha_quanto_tempo(double minutosAtras, string texto) =>
        Assert.Equal(texto, RegrasNotificacoes.HaQuanto(Agora.AddMinutes(-minutosAtras), Agora));

    [Fact]
    public void Textos_dos_estados()
    {
        Assert.Null(RegrasNotificacoes.Texto(EstadoNotificacoes.Ligado));
        Assert.Equal("Ligue o modo de desenvolvedor do Windows (Configurações > Sistema > Para desenvolvedores) para o Vitral mostrar as notificações.",
            RegrasNotificacoes.Texto(EstadoNotificacoes.SemIdentidade));
        Assert.Equal("O Windows não deixou o Vitral ler as notificações. Permita em Configurações > Privacidade > Notificações.",
            RegrasNotificacoes.Texto(EstadoNotificacoes.SemPermissao));
    }

    [Fact]
    public void Nao_registra_de_novo_com_a_mesma_pasta()
    {
        Assert.False(RegrasNotificacoes.PrecisaRegistrar(@"C:\a\current", @"C:\a\current"));
        Assert.False(RegrasNotificacoes.PrecisaRegistrar(@"C:\A\Current\", @"c:\a\current"));
        Assert.True(RegrasNotificacoes.PrecisaRegistrar(@"C:\a\current", @"G:\dev\vitral\bin"));
        Assert.True(RegrasNotificacoes.PrecisaRegistrar(null, @"C:\a\current"));
    }

    [Fact]
    public void Cala_os_apps_menos_o_Claude_e_os_ja_calados()
    {
        var cala = RegrasNotificacoes.ParaCalar([("Chrome", null), ("Microsoft.SkyDrive.Desktop", 0), ("Claude_pzs8sxrjxfjjc!Claude", null), ("Email", 1)]);
        Assert.Equal(["Chrome", "Email"], cala);
    }

    [Fact]
    public void Restaura_o_que_ficou_ligado()
    {
        Assert.Equal(["A", "B"], RegrasNotificacoes.ParaDevolver(["A", "B"]));
        Assert.Empty(RegrasNotificacoes.ParaDevolver(null));
    }

    [Fact]
    public void Muitas_e_sem_icone()
    {
        var muitas = Enumerable.Range(1, 150).Select(i => N((uint)i, $"App{i % 20}", i)).ToList();
        muitas.Add(new NotificacaoApp(999, "SemNome", "", null, null, Agora));
        var relogio = Stopwatch.StartNew();
        var grupos = RegrasNotificacoes.Agrupar(muitas);
        Assert.True(relogio.ElapsedMilliseconds < 50, $"{relogio.ElapsedMilliseconds} ms");
        Assert.Equal(21, grupos.Count);
        Assert.Equal("SemNome", grupos.Single(g => g.AppId == "SemNome").NomeApp);
    }
}

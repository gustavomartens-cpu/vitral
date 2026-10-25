using System.Collections.Specialized;
using Vitral.Ui.Doca;

namespace Vitral.Ui.Tests;

internal sealed class JanelasFalsas : IFonteJanelas
{
    public IReadOnlyList<JanelaApp> Janelas { get; set; } = [];
    public IntPtr Ativa { get; set; }
    public event Action? Mudou;
    public List<string> Acoes { get; } = [];
    public void Ativar(IntPtr h) => Acoes.Add($"ativar:{h}");
    public void Minimizar(IntPtr h) => Acoes.Add($"minimizar:{h}");
    public void Fechar(IntPtr h) => Acoes.Add($"fechar:{h}");
    public void Disparar() => Mudou?.Invoke();
}

internal sealed class FixadosFalsos : IFonteFixados
{
    public IReadOnlyList<Fixado> Lista { get; set; } = [];
    public HashSet<string> Inexistentes { get; } = [];
    public List<IReadOnlyList<Fixado>> Salvos { get; } = [];
    public bool Existe(Fixado f) => !Inexistentes.Contains(f.Caminho);
    public void Salvar(IReadOnlyList<Fixado> lista) { Salvos.Add(lista); Lista = lista; }
}

internal sealed class AtalhosFalsos : IAtalhosDoca
{
    public List<string> Chamadas { get; } = [];
    public void Iniciar() => Chamadas.Add("iniciar");
    public void Pesquisar() => Chamadas.Add("pesquisar");
    public void Abrir(Fixado f) => Chamadas.Add($"abrir:{f.Nome}");
    public void NovaJanela(string executavel) => Chamadas.Add($"nova:{executavel}");
}

public sealed class DocaTests
{
    private static JanelaApp J(int h, string titulo, string? appId, string exe, bool atencao = false) =>
        new(h, titulo, appId, exe, false, atencao, null);

    private readonly JanelasFalsas _janelas = new();
    private readonly FixadosFalsos _fixados = new();
    private readonly AtalhosFalsos _atalhos = new();
    private readonly DialogosFalsos _dialogos = new();

    private DocaViewModel Criar() => new(_janelas, _fixados, _atalhos, _dialogos);

    [Fact]
    public void Agrupa_por_app_id_e_por_executavel()
    {
        var grupos = GrupoApps.Montar([],
        [
            J(1, "Aba 1", "Chrome", @"C:\Chrome\chrome.exe"),
            J(2, "Aba 2", "Chrome", @"C:\Chrome\chrome.exe"),
            J(3, "Calculadora", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", @"C:\Windows\ApplicationFrameHost.exe"),
            J(4, "Bloco de notas", null, @"C:\Windows\notepad.exe"),
        ]);
        Assert.Equal(3, grupos.Count);
        Assert.Equal(2, grupos[0].Janelas.Count);
    }

    [Fact]
    public void Ordem_fixados_depois_abertos()
    {
        var fixados = new List<Fixado> { new(@"C:\l\Steam.lnk", null, "Steam", @"C:\Steam\steam.exe"), new(@"C:\l\Spotify.lnk", "Spotify", "Spotify") };
        var grupos = GrupoApps.Montar(fixados, [J(9, "Notas", null, @"C:\n\notepad.exe"), J(8, "Música", "Spotify", @"C:\s\Spotify.exe")]);
        Assert.Equal(["Steam", "Spotify", "Notas"], grupos.Select(g => g.Nome));
        Assert.Empty(grupos[0].Janelas);
        Assert.Single(grupos[1].Janelas);
    }

    [Fact]
    public void Fixado_casa_pelo_executavel_do_alvo()
    {
        var fixados = new List<Fixado> { new(@"C:\l\Steam.lnk", null, "Steam", @"C:\Steam\steam.exe") };
        var grupos = GrupoApps.Montar(fixados, [J(5, "Steam", null, @"c:\steam\STEAM.EXE")]);
        Assert.Single(grupos);
        Assert.Single(grupos[0].Janelas);
    }

    [Fact]
    public void Decidir_a_acao_do_clique()
    {
        var vazio = new GrupoApp("x", "X", null, []);
        var um = new GrupoApp("a", "A", null, [J(1, "a", null, "a.exe")]);
        var dois = new GrupoApp("b", "B", null, [J(2, "b1", null, "b.exe"), J(3, "b2", null, "b.exe")]);
        Assert.Equal(TipoAcao.Abrir, GrupoApps.Decidir(vazio, 0).Tipo);
        Assert.Equal(new AcaoClique(TipoAcao.Minimizar, 1), GrupoApps.Decidir(um, 1));
        Assert.Equal(new AcaoClique(TipoAcao.Ativar, 1), GrupoApps.Decidir(um, 99));
        Assert.Equal(TipoAcao.Escolher, GrupoApps.Decidir(dois, 2).Tipo);
    }

    [Fact]
    public void Indicadores()
    {
        _janelas.Janelas = [J(1, "a", "A", "a.exe"), J(2, "b", "B", "b.exe", atencao: true)];
        _janelas.Ativa = 1;
        var vm = Criar();
        Assert.True(vm.Botoes[0].Ativo);
        Assert.True(vm.Botoes[0].Aberto);
        Assert.False(vm.Botoes[1].Ativo);
        Assert.True(vm.Botoes[1].PedindoAtencao);
    }

    [Fact]
    public void Sincroniza_sem_reset()
    {
        _janelas.Janelas = [J(1, "a", "A", "a.exe")];
        var vm = Criar();
        var primeiro = vm.Botoes[0];
        var acoes = new List<NotifyCollectionChangedAction>();
        vm.Botoes.CollectionChanged += (_, e) => acoes.Add(e.Action);

        _janelas.Janelas = [J(1, "a", "A", "a.exe"), J(2, "b", "B", "b.exe")];
        _janelas.Ativa = 2;
        _janelas.Disparar();

        Assert.Equal([NotifyCollectionChangedAction.Add], acoes);
        Assert.Same(primeiro, vm.Botoes[0]);
        Assert.True(vm.Botoes[1].Ativo);
    }

    [Fact]
    public void Clique_executa_a_acao()
    {
        _janelas.Janelas = [J(1, "a", "A", "a.exe"), J(2, "b1", "B", "b.exe"), J(3, "b2", "B", "b.exe")];
        _janelas.Ativa = 1;
        var vm = Criar();
        BotaoAppViewModel? escolha = null;
        vm.PediuEscolha += b => escolha = b;

        vm.ClicarCommand.Execute(vm.Botoes[0]);
        vm.ClicarCommand.Execute(vm.Botoes[1]);

        Assert.Equal(["minimizar:1"], _janelas.Acoes);
        Assert.Same(vm.Botoes[1], escolha);
    }

    [Fact]
    public void Iniciar_e_pesquisar_chegam_nos_atalhos()
    {
        var vm = Criar();
        vm.IniciarCommand.Execute(null);
        vm.PesquisarCommand.Execute(null);
        Assert.Equal(["iniciar", "pesquisar"], _atalhos.Chamadas);
    }
}

using System.Collections.Specialized;
using Vitral.Core.Doca;
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
    public object? Icone(Fixado f) => null;
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
    public void Fixado_de_app_da_loja_casa_com_a_janela_da_versao_nova()
    {
        var fixados = new List<Fixado> { new(@"C:\Program Files\WindowsApps\Claude_4.29.0.0_x64__pzs8sxrjxfjjc\app\claude.exe", null, "Claude",
            @"C:\Program Files\WindowsApps\Claude_4.29.0.0_x64__pzs8sxrjxfjjc\app\claude.exe") };
        var grupos = GrupoApps.Montar(fixados, [J(5, "Claude", null, @"C:\Program Files\WindowsApps\Claude_4.4212.0.0_x64__pzs8sxrjxfjjc\app\claude.exe")]);
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

public sealed class FixadosDocaTests
{
    private static JanelaApp J(int h, string titulo, string? appId, string exe) => new(h, titulo, appId, exe, false, false, null);

    private readonly JanelasFalsas _janelas = new();
    private readonly FixadosFalsos _fixados = new();
    private readonly AtalhosFalsos _atalhos = new();
    private readonly DialogosFalsos _dialogos = new();

    private DocaViewModel Criar() => new(_janelas, _fixados, _atalhos, _dialogos);

    private static readonly Fixado Steam = new(@"C:\l\Steam.lnk", null, "Steam", @"C:\Steam\steam.exe");
    private static readonly Fixado Spotify = new(@"C:\l\Spotify.lnk", "Spotify", "Spotify");
    private static readonly Fixado Notas = new(@"C:\l\Notas.lnk", null, "Notas", @"C:\n\notepad.exe");

    [Fact]
    public void Fixar_e_desafixar_salvam_a_lista()
    {
        _janelas.Janelas = [J(1, "Bloco de notas", null, @"C:\n\notepad.exe")];
        var vm = Criar();

        vm.FixarCommand.Execute(vm.Botoes[0]);
        var salvo = Assert.Single(_fixados.Salvos[^1]);
        Assert.Equal(@"C:\n\notepad.exe", salvo.Caminho);
        Assert.Equal(@"C:\n\notepad.exe", salvo.Alvo);
        Assert.True(vm.Botoes[0].Fixado is not null);

        vm.DesafixarCommand.Execute(vm.Botoes[0]);
        Assert.Empty(_fixados.Salvos[^1]);
    }

    [Fact]
    public void Mover_reordena_so_os_fixados()
    {
        _fixados.Lista = [Steam, Spotify, Notas];
        _janelas.Janelas = [J(7, "Calculadora", "Calc", @"C:\c\calc.exe")];
        var vm = Criar();

        vm.Mover(0, 2);
        Assert.Equal(["Spotify", "Notas", "Steam"], _fixados.Salvos[^1].Select(f => f.Nome));
        Assert.Equal(["Spotify", "Notas", "Steam", "Calculadora"], vm.Botoes.Select(b => b.Nome));

        vm.Mover(0, 3); // destino fora dos fixados: não faz nada
        Assert.Single(_fixados.Salvos);
    }

    [Fact]
    public void Fixado_sem_alvo_avisa()
    {
        _fixados.Lista = [Steam];
        _fixados.Inexistentes.Add(Steam.Caminho);
        var vm = Criar();

        vm.ClicarCommand.Execute(vm.Botoes[0]);

        Assert.Equal(["Atalho não encontrado"], _dialogos.Avisos);
        Assert.Empty(_atalhos.Chamadas);
    }

    [Fact]
    public void Desafixar_app_aberto_mantem_o_botao()
    {
        _fixados.Lista = [Spotify];
        _janelas.Janelas = [J(8, "Música", "Spotify", @"C:\s\Spotify.exe")];
        var vm = Criar();
        var botao = vm.Botoes[0];

        vm.DesafixarCommand.Execute(botao);

        Assert.Same(botao, Assert.Single(vm.Botoes));
        Assert.Null(botao.Fixado);
        Assert.True(botao.Aberto);
    }
}

public sealed class MenuDocaTests
{
    private static JanelaApp J(int h, string titulo, string? appId, string exe) => new(h, titulo, appId, exe, false, false, null);

    private readonly JanelasFalsas _janelas = new();
    private readonly FixadosFalsos _fixados = new();
    private readonly AtalhosFalsos _atalhos = new();
    private readonly DialogosFalsos _dialogos = new();

    private DocaViewModel Criar() => new(_janelas, _fixados, _atalhos, _dialogos);

    [Fact]
    public void Menu_de_fixado_aberto_com_uma_janela()
    {
        _fixados.Lista = [new(@"C:\l\Spotify.lnk", "Spotify", "Spotify")];
        _janelas.Janelas = [J(8, "Música", "Spotify", @"C:\s\Spotify.exe")];
        var vm = Criar();
        Assert.Equal(["Desafixar", "Abrir nova janela", "Fechar janela"], vm.Menu(vm.Botoes[0]).Select(i => i.Texto));
    }

    [Fact]
    public void Menu_de_app_nao_fixado_com_duas_janelas()
    {
        _janelas.Janelas = [J(1, "a", "B", @"C:\b\b.exe"), J(2, "b", "B", @"C:\b\b.exe")];
        var vm = Criar();
        Assert.Equal(["Fixar", "Abrir nova janela", "Fechar todas"], vm.Menu(vm.Botoes[0]).Select(i => i.Texto));
    }

    [Fact]
    public void Menu_sem_executavel_nao_oferece_nova_janela()
    {
        _janelas.Janelas = [J(3, "x", null, "")];
        var vm = Criar();
        Assert.DoesNotContain("Abrir nova janela", vm.Menu(vm.Botoes[0]).Select(i => i.Texto));
    }

    [Fact]
    public void Fechar_todas_fecha_cada_janela()
    {
        _janelas.Janelas = [J(1, "a", "B", @"C:\b\b.exe"), J(2, "b", "B", @"C:\b\b.exe")];
        var vm = Criar();
        vm.FecharTodasCommand.Execute(vm.Botoes[0]);
        Assert.Equal(["fechar:1", "fechar:2"], _janelas.Acoes);
    }

    [Fact]
    public void Escolher_janela_ativa()
    {
        _janelas.Janelas = [J(1, "a", "B", @"C:\b\b.exe"), J(2, "b", "B", @"C:\b\b.exe")];
        var vm = Criar();
        vm.EscolherJanelaCommand.Execute(_janelas.Janelas[1]);
        Assert.Equal(["ativar:2"], _janelas.Acoes);
    }

    [Fact]
    public void Nova_janela_usa_o_atalho_do_fixado_ou_o_executavel()
    {
        _fixados.Lista = [new(@"C:\l\Spotify.lnk", "Spotify", "Spotify")];
        _janelas.Janelas = [J(1, "a", "B", @"C:\b\b.exe")];
        var vm = Criar();
        vm.NovaJanelaCommand.Execute(vm.Botoes[0]);
        vm.NovaJanelaCommand.Execute(vm.Botoes[1]);
        Assert.Equal(["abrir:Spotify", @"nova:C:\b\b.exe"], _atalhos.Chamadas);
    }
}

public sealed class DocaRevisaoTests
{
    private static JanelaApp J(int h, string titulo, string? appId, string exe) => new(h, titulo, appId, exe, false, false, null);

    [Fact]
    public void App_web_com_id_proprio_nao_cai_no_fixado_do_navegador()
    {
        var fixados = new List<Fixado> { new(@"C:\l\Edge.lnk", "MSEdge", "Edge", @"C:\e\msedge.exe") };
        var grupos = GrupoApps.Montar(fixados,
        [
            J(1, "Edge", "MSEdge", @"C:\e\msedge.exe"),
            J(2, "Outlook (PWA)", "MSEdge._crx_abc", @"C:\e\msedge.exe"),
            J(3, "Edge sem id", null, @"C:\e\msedge.exe"),
        ]);
        Assert.Equal(2, grupos.Count);
        Assert.Equal([1, 3], grupos[0].Janelas.Select(j => (int)j.Handle));
    }

    [Fact]
    public void Atualizar_sem_mudanca_nao_troca_a_lista_de_janelas()
    {
        var janelas = new JanelasFalsas { Janelas = [J(1, "a", "A", "a.exe")] };
        var vm = new DocaViewModel(janelas, new FixadosFalsos(), new AtalhosFalsos(), new DialogosFalsos());
        var mudancas = new List<string?>();
        vm.Botoes[0].PropertyChanged += (_, e) => mudancas.Add(e.PropertyName);

        janelas.Janelas = [J(1, "a", "A", "a.exe")]; // mesma janela, lista nova
        janelas.Disparar();

        Assert.DoesNotContain(nameof(BotaoAppViewModel.Janelas), mudancas);
    }
}

public sealed class MenuDocaIconesTests
{
    [Fact]
    public void Menu_da_doca_tem_icones_e_perigo()
    {
        var janelas = new JanelasFalsas { Janelas = [new(8, "Música", "Spotify", @"C:\s\Spotify.exe", false, false, null)] };
        var fixados = new FixadosFalsos { Lista = [new(@"C:\l\Spotify.lnk", "Spotify", "Spotify")] };
        var vm = new DocaViewModel(janelas, fixados, new AtalhosFalsos(), new DialogosFalsos());

        var menu = vm.Menu(vm.Botoes[0]);

        Assert.Equal(["\uE718", "\uE710", "\uE711"], menu.Select(i => i.Icone));
        Assert.Equal([false, false, true], menu.Select(i => i.Perigo));
    }
}

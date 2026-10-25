using System.Collections.Specialized;
using Vitral.Core.Configuracao;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Tests;

/// Testes dos achados Critical e Important da revisão final da fase 1d.
public sealed class RevisaoFinal1dTests : IDisposable
{
    private readonly Ambiente _amb = new();
    private readonly DialogosFalsos _dialogos = new();
    private readonly RelogioFalso _relogio = new();

    public void Dispose() => _amb.Dispose();

    private static Perfil Claro(string nome = "Claro") =>
        new(1, nome, null, new AjustesNativosPerfil(null, Modo.Claro, null, null, null, null, null, null));

    private PrincipalViewModel Criar() => new(_amb.Servico, _amb.Perfis, _dialogos, _relogio);

    private void GravarPerfilCorrompido(string pasta)
    {
        string dir = Path.Combine(_amb.Perfis.Pasta, pasta);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "perfil.json"), "{lixo");
    }

    // Critical 1: perfil inválido não pode impedir o app de abrir

    [Fact]
    public void Perfil_corrompido_nao_impede_abrir_nem_o_login()
    {
        _amb.Perfis.Salvar(Claro());
        GravarPerfilCorrompido("Quebrado");
        _amb.Config.Gravar(ConfigApp.Padrao with { PerfilAtivo = "Quebrado" });

        var estado = _amb.Servico.AoIniciar(inicializacaoDoWindows: true);
        var vm = Criar();

        Assert.NotNull(estado.Aviso);
        Assert.Contains("Claro", vm.Perfis);
        Assert.Equal(0u, _amb.Apps());
    }

    // Important 2: Desfazer/Original bloqueados durante a contagem

    [Fact]
    public void Desfazer_durante_a_contagem_nao_mexe_e_explica()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.TestarCommand.Execute(null);

        vm.DesfazerCommand.Execute(null);

        Assert.Equal(1u, _amb.Apps()); // continua no visual do teste
        Assert.Contains("teste", vm.Mensagem);
        vm.Contagem!.ManterCommand.Execute(null);
        Assert.Equal("Claro", vm.PerfilAtivo);
        Assert.Equal(1u, _amb.Apps()); // ativo e aplicado batem
    }

    [Fact]
    public void Original_durante_a_contagem_nao_mexe()
    {
        _amb.Perfis.Salvar(Claro());
        _dialogos.RespostaConfirmar = true;
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.TestarCommand.Execute(null);

        vm.OriginalCommand.Execute(null);

        Assert.Empty(_dialogos.Perguntas);
        Assert.Equal(1u, _amb.Apps());
    }

    // Important 3: atualizar a lista não pode passar por "Reset" (que apaga o editor na janela)

    [Fact]
    public void Atualizar_lista_nao_reseta_a_colecao()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        var acoes = new List<NotifyCollectionChangedAction>();
        vm.Perfis.CollectionChanged += (_, e) => acoes.Add(e.Action);
        _amb.Perfis.Salvar(Claro("B"));

        vm.AtualizarLista(vm.Selecionado);

        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, acoes);
        Assert.Equal(["A", "B"], vm.Perfis);
    }

    [Fact]
    public void Nomes_para_o_menu_nao_mexem_no_editor()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.Editor.Transparencia = Escolha.Nao; // editado e não salvo

        var nomes = vm.NomesParaMenu();

        Assert.Equal(["Claro"], nomes);
        Assert.Equal(Escolha.Nao, vm.Editor.Transparencia);
    }

    // Important 4: aplicação parcial chega ao usuário pela bandeja e no login

    [Fact]
    public void Troca_pela_bandeja_parcial_devolve_o_aviso()
    {
        _amb.Perfis.Salvar(Claro());
        _amb.Registro.Bloqueados.Add((Ambiente.Personalize, "AppsUseLightTheme"));
        var vm = Criar();

        string? aviso = vm.TrocarPelaBandeja("Claro");

        Assert.NotNull(aviso);
        Assert.Contains("AppsUseLightTheme", aviso);
    }

    [Fact]
    public void Login_parcial_vira_aviso()
    {
        _amb.Perfis.Salvar(Claro());
        _amb.Registro.Bloqueados.Add((Ambiente.Personalize, "AppsUseLightTheme"));
        _amb.Config.Gravar(ConfigApp.Padrao with { PerfilAtivo = "Claro" });

        var estado = _amb.Servico.AoIniciar(inicializacaoDoWindows: true);

        Assert.NotNull(estado.Aviso);
        Assert.Contains("AppsUseLightTheme", estado.Aviso);
    }

    // Important 5: erros não derrubam a janela; retrato ilegível não causa queda a cada abertura

    [Fact]
    public void Retrato_do_teste_ilegivel_limpa_a_marca_e_avisa()
    {
        _amb.Perfis.Salvar(Claro());
        _amb.Servico.IniciarTeste("Claro");
        string retrato = _amb.Config.Ler().TesteEmAndamento!.Retrato;
        File.WriteAllText(Path.Combine(_amb.Raiz, "retratos", retrato), "{lixo");

        var estado = _amb.Servico.AoIniciar(inicializacaoDoWindows: false);

        Assert.NotNull(estado.Aviso);
        Assert.Null(_amb.Config.Ler().TesteEmAndamento);
    }

    [Fact]
    public void Desfazer_com_retrato_ilegivel_avisa_sem_lancar()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.AplicarCommand.Execute(null);
        var ultimo = Directory.GetFiles(Path.Combine(_amb.Raiz, "retratos")).Where(f => !f.EndsWith("original.json")).Max()!;
        File.WriteAllText(ultimo, "{lixo");

        vm.DesfazerCommand.Execute(null);

        Assert.NotEmpty(_dialogos.Avisos);
    }

    [Fact]
    public void Importar_arquivo_inexistente_avisa_sem_lancar()
    {
        _dialogos.ArquivoParaAbrir = Path.Combine(_amb.Raiz, "nao-existe.vitral");
        var vm = Criar();
        vm.ImportarCommand.Execute(null);
        Assert.NotEmpty(_dialogos.Avisos);
    }
}

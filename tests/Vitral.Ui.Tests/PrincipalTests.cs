using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Ui.Tests;

public sealed class PrincipalTests : IDisposable
{
    private readonly Ambiente _amb = new();
    private readonly DialogosFalsos _dialogos = new();
    private readonly RelogioFalso _relogio = new();

    public void Dispose() => _amb.Dispose();

    private static Perfil Claro(string nome = "Claro") =>
        new(1, nome, null, new AjustesNativosPerfil(null, Modo.Claro, null, null, null, null, null, null));

    private PrincipalViewModel Criar() => new(_amb.Servico, _amb.Perfis, _dialogos, _relogio);

    [Fact]
    public void Lista_os_perfis_e_carrega_o_selecionado_no_editor()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        Assert.Contains("Claro", vm.Perfis);
        vm.Selecionado = "Claro";
        Assert.Equal(ModoOpcional.Claro, vm.Editor.ModoApps);
    }

    [Fact]
    public void Testar_e_manter_marca_o_perfil_ativo()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";

        vm.TestarCommand.Execute(null);
        Assert.NotNull(vm.Contagem);
        Assert.Equal(20, vm.Contagem!.Restantes);
        Assert.Equal(1u, _amb.Apps());

        vm.Contagem.ManterCommand.Execute(null);

        Assert.Equal("Claro", vm.PerfilAtivo);
        Assert.Null(vm.Contagem);
        Assert.Equal(1u, _amb.Apps());
    }

    [Fact]
    public void Testar_sem_resposta_volta_ao_anterior()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";

        vm.TestarCommand.Execute(null);
        _relogio.Tique(20);

        Assert.Equal(0u, _amb.Apps());
        Assert.Null(vm.Contagem);
        Assert.Contains("Voltou ao anterior", vm.Mensagem);
        Assert.Null(vm.PerfilAtivo);
    }

    [Fact]
    public void Testar_salva_antes_o_que_esta_no_editor()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.Editor.Transparencia = Escolha.Nao;

        vm.TestarCommand.Execute(null);

        Assert.False(_amb.Perfis.Carregar("Claro").Nativo!.Transparencia);
    }

    [Fact]
    public void Salvar_grava_as_formas()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.EditorFormas.FormatoNotch = FormatoNotch.Ilha;
        vm.SalvarPerfilCommand.Execute(null);
        Assert.Equal(new FormasPerfil(Notch: new FormasNotch(FormatoNotch.Ilha)), _amb.Perfis.Carregar("Claro").Formas);
        vm.Selecionado = null;
        vm.Selecionado = "Claro";
        Assert.Equal(FormatoNotch.Ilha, vm.EditorFormas.FormatoNotch);
    }

    [Fact]
    public void Salvar_com_cor_invalida_avisa_e_nao_salva()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.Editor.CorDestaque = "azul";

        vm.SalvarPerfilCommand.Execute(null);

        Assert.Single(_dialogos.Avisos);
        Assert.Null(_amb.Perfis.Carregar("Claro").Nativo!.CorDestaque);
    }

    [Fact]
    public void Aplicar_com_item_bloqueado_cita_o_item()
    {
        _amb.Perfis.Salvar(Claro());
        _amb.Registro.Bloqueados.Add((Ambiente.Personalize, "AppsUseLightTheme"));
        var vm = Criar();
        vm.Selecionado = "Claro";

        vm.AplicarCommand.Execute(null);

        Assert.Contains("AppsUseLightTheme", vm.Mensagem);
    }

    [Fact]
    public void Novo_perfil_pede_nome_e_seleciona()
    {
        _dialogos.RespostaTexto = "Meu novo";
        var vm = Criar();
        vm.NovoCommand.Execute(null);
        Assert.Contains("Meu novo", vm.Perfis);
        Assert.Equal("Meu novo", vm.Selecionado);
    }

    [Fact]
    public void Importar_nome_repetido_sem_confirmar_mantem_o_local()
    {
        string zip = PrepararPacoteComClaroDiferente();
        _dialogos.ArquivoParaAbrir = zip;
        _dialogos.RespostaConfirmar = false;
        var vm = Criar();

        vm.ImportarCommand.Execute(null);

        Assert.Single(_dialogos.Perguntas);
        Assert.Equal(Modo.Claro, _amb.Perfis.Carregar("Claro").Nativo!.ModoApps);
    }

    [Fact]
    public void Importar_nome_repetido_confirmando_substitui()
    {
        string zip = PrepararPacoteComClaroDiferente();
        _dialogos.ArquivoParaAbrir = zip;
        _dialogos.RespostaConfirmar = true;
        var vm = Criar();

        vm.ImportarCommand.Execute(null);

        Assert.Equal(Modo.Escuro, _amb.Perfis.Carregar("Claro").Nativo!.ModoApps);
    }

    [Fact]
    public void Desfazer_depois_de_aplicar_volta()
    {
        _amb.Perfis.Salvar(Claro());
        var vm = Criar();
        vm.Selecionado = "Claro";
        vm.AplicarCommand.Execute(null);
        Assert.Equal(1u, _amb.Apps());

        vm.DesfazerCommand.Execute(null);

        Assert.Equal(0u, _amb.Apps());
    }

    /// Perfil local "Claro" (apps claro) e um pacote com outro "Claro" (apps escuro).
    private string PrepararPacoteComClaroDiferente()
    {
        _amb.Perfis.Salvar(Claro());
        var outro = new RepositorioPerfis(Path.Combine(_amb.Raiz, "outro"));
        outro.Salvar(new Perfil(1, "Claro", null, new AjustesNativosPerfil(null, Modo.Escuro, null, null, null, null, null, null)));
        string zip = Path.Combine(_amb.Raiz, "p.vitral");
        Pacote.Exportar(outro, ["Claro"], zip, "0.1.0", "x");
        return zip;
    }

    // ---- Repaginação (spec da interface §3.2, §3.3, §3.6) ----

    private void Ativo(string nome) => _amb.Config.Gravar(_amb.Config.Ler() with { PerfilAtivo = nome });

    [Fact]
    public void Abre_no_perfil_ativo()
    {
        _amb.Perfis.Salvar(Claro("A"));
        _amb.Perfis.Salvar(Claro("B"));
        Ativo("B");
        Assert.Equal("B", Criar().Selecionado);
    }

    [Fact]
    public void Abre_no_primeiro_sem_ativo_valido()
    {
        _amb.Perfis.Salvar(Claro("A"));
        _amb.Perfis.Salvar(Claro("B"));
        Ativo("Apagado");
        var vm = Criar();
        Assert.Equal(vm.Perfis[0], vm.Selecionado);
    }

    [Fact]
    public void Sem_perfis_nada_selecionado() => Assert.Null(Criar().Selecionado);

    [Fact]
    public void Editar_liga_as_alteracoes_e_salvar_desliga()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        Assert.False(vm.TemAlteracoes);
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        Assert.True(vm.TemAlteracoes);
        vm.SalvarPerfilCommand.Execute(null);
        Assert.False(vm.TemAlteracoes);
    }

    [Fact]
    public void Marcar_icone_da_barra_liga_as_alteracoes()
    {
        _amb.Perfis.Salvar(Claro("A") with { BarraTarefas = new BarraTarefasPerfil(IconesVisiveis: ["x.exe"]) });
        var vm = Criar();
        Assert.False(vm.TemAlteracoes);
        vm.EditorBarraTarefas.Icones[0].Visivel = false;
        Assert.True(vm.TemAlteracoes);
    }

    [Fact]
    public void Ajuste_da_aparencia_que_nao_muda_a_previa_liga_as_alteracoes()
    {
        _amb.Perfis.Salvar(Claro("A") with { Aparencia = new AparenciaPerfil(TemaAparencia.VidroEscuro) });
        var vm = Criar();
        // Com a transparência do Windows desligada, a transparência do perfil não muda a prévia.
        vm.EditorAparencia.LerWindows = () => new VisualWindows(true, "#0078D4", false);
        vm.EditorAparencia.Recalcular();
        Assert.False(vm.TemAlteracoes);
        vm.EditorAparencia.Geral.Transparencia = 40;
        Assert.True(vm.TemAlteracoes);
    }

    [Fact]
    public void Descartar_desliga_as_alteracoes()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        vm.DescartarCommand.Execute(null);
        Assert.False(vm.TemAlteracoes);
        Assert.Equal(ModoOpcional.Claro, vm.Editor.ModoApps);
    }

    [Fact]
    public void Aplicar_salva_e_limpa_as_alteracoes()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        vm.AplicarCommand.Execute(null);
        Assert.False(vm.TemAlteracoes);
        Assert.Equal(Modo.Escuro, _amb.Perfis.Carregar("A").Nativo!.ModoApps);
    }

    [Fact]
    public void Troca_com_pendencia_e_Salvar_grava_o_anterior()
    {
        _amb.Perfis.Salvar(Claro("A"));
        _amb.Perfis.Salvar(Claro("B"));
        Ativo("A");
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        _dialogos.RespostaPendente = RespostaPendente.Salvar;
        vm.Selecionado = "B";
        Assert.Equal(["A"], _dialogos.PerguntasDeSalvar);
        Assert.Equal(Modo.Escuro, _amb.Perfis.Carregar("A").Nativo!.ModoApps);
        Assert.Equal("B", vm.Selecionado);
        Assert.False(vm.TemAlteracoes);
    }

    [Fact]
    public void Troca_com_pendencia_e_Descartar_perde_a_mudanca()
    {
        _amb.Perfis.Salvar(Claro("A"));
        _amb.Perfis.Salvar(Claro("B"));
        Ativo("A");
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        _dialogos.RespostaPendente = RespostaPendente.Descartar;
        vm.Selecionado = "B";
        Assert.Equal(Modo.Claro, _amb.Perfis.Carregar("A").Nativo!.ModoApps);
        Assert.Equal("B", vm.Selecionado);
        Assert.False(vm.TemAlteracoes);
    }

    [Fact]
    public void Troca_com_pendencia_e_Cancelar_fica_onde_estava()
    {
        _amb.Perfis.Salvar(Claro("A"));
        _amb.Perfis.Salvar(Claro("B"));
        Ativo("A");
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        _dialogos.RespostaPendente = RespostaPendente.Cancelar;
        vm.Selecionado = "B";
        Assert.Equal("A", vm.Selecionado);
        Assert.Equal(ModoOpcional.Escuro, vm.Editor.ModoApps);
        Assert.True(vm.TemAlteracoes);
    }

    [Fact]
    public void Troca_sem_pendencia_nao_pergunta()
    {
        _amb.Perfis.Salvar(Claro("A"));
        _amb.Perfis.Salvar(Claro("B"));
        Ativo("A");
        var vm = Criar();
        vm.Selecionado = "B";
        Assert.Empty(_dialogos.PerguntasDeSalvar);
        Assert.Equal("B", vm.Selecionado);
    }

    [Fact]
    public void Opcao_escondida_mantem_o_valor_ao_salvar()
    {
        _amb.Perfis.Salvar(Claro("A") with { BarraTarefas = new BarraTarefasPerfil(OcultarAutomaticamente: true) });
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        vm.SalvarPerfilCommand.Execute(null);
        Assert.True(_amb.Perfis.Carregar("A").BarraTarefas!.OcultarAutomaticamente);
    }

    [Fact]
    public void Mensagem_espera_a_janela_aparecer_para_contar_os_6s()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar(); // como ao iniciar com o Windows: a janela ainda nem existe
        vm.DescartarCommand.Execute(null);
        _relogio.Tique(10);
        Assert.NotNull(vm.Mensagem);
        vm.JanelaVisivel = true;
        _relogio.Tique(5);
        Assert.NotNull(vm.Mensagem);
        _relogio.Tique(1);
        Assert.Null(vm.Mensagem);
    }

    [Fact]
    public void Novo_com_pendencia_e_Cancelar_nao_cria_o_perfil()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        _dialogos.RespostaTexto = "Novo";
        _dialogos.RespostaPendente = RespostaPendente.Cancelar;
        vm.NovoCommand.Execute(null);
        Assert.DoesNotContain("Novo", vm.Perfis);
        Assert.Equal("A", vm.Selecionado);
        Assert.True(vm.TemAlteracoes);
    }

    [Fact]
    public void Novo_com_pendencia_e_Descartar_cria_e_abre_perguntando_uma_vez()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        _dialogos.RespostaTexto = "Novo";
        _dialogos.RespostaPendente = RespostaPendente.Descartar;
        vm.NovoCommand.Execute(null);
        Assert.Equal("Novo", vm.Selecionado);
        Assert.Single(_dialogos.PerguntasDeSalvar);
        Assert.Equal(Modo.Claro, _amb.Perfis.Carregar("A").Nativo?.ModoApps);
    }

    [Fact]
    public void Importar_com_pendencia_e_Cancelar_nao_importa()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        _dialogos.ArquivoParaAbrir = Path.Combine(_amb.Raiz, "nao-existe.vitral");
        _dialogos.RespostaPendente = RespostaPendente.Cancelar;
        vm.ImportarCommand.Execute(null);
        Assert.Empty(_dialogos.Avisos); // nem tentou abrir o arquivo
        Assert.True(vm.TemAlteracoes);
    }

    [Fact]
    public void Importar_por_cima_do_perfil_aberto_recarrega_o_editor()
    {
        string pacote = Path.Combine(_amb.Raiz, "a.vitral");
        _amb.Perfis.Salvar(Claro("A") with { Nativo = new AjustesNativosPerfil(null, Modo.Escuro, null, null, null, null, null, null) });
        Pacote.Exportar(_amb.Perfis, ["A"], pacote, "0", "0");
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        Assert.Equal(ModoOpcional.Claro, vm.Editor.ModoApps);
        _dialogos.ArquivoParaAbrir = pacote;
        _dialogos.RespostaConfirmar = true;
        vm.ImportarCommand.Execute(null);
        Assert.Equal(ModoOpcional.Escuro, vm.Editor.ModoApps);
        Assert.False(vm.TemAlteracoes);
    }

    [Fact]
    public void Falha_ao_abrir_um_perfil_volta_ao_anterior()
    {
        _amb.Perfis.Salvar(Claro("A"));
        _amb.Perfis.Salvar(Claro("B"));
        foreach (var f in Directory.EnumerateFiles(Path.Combine(_amb.Raiz, "perfis"), "perfil.json", SearchOption.AllDirectories))
            if (File.ReadAllText(f).Contains("\"B\"")) File.WriteAllText(f, "{ quebrado");
        var vm = Criar();
        vm.Selecionado = "A";
        vm.Selecionado = "B";
        Assert.Equal("A", vm.Selecionado);
        Assert.NotNull(vm.Mensagem);
    }

    [Theory]
    [InlineData(RespostaPendente.Cancelar, false)]
    [InlineData(RespostaPendente.Descartar, true)]
    [InlineData(RespostaPendente.Salvar, true)]
    public void Sair_com_pendencia_pergunta(RespostaPendente resposta, bool sai)
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        vm.Editor.ModoApps = ModoOpcional.Escuro;
        _dialogos.RespostaPendente = resposta;
        Assert.Equal(sai, vm.PodeSair());
        Assert.Equal(resposta == RespostaPendente.Salvar ? Modo.Escuro : Modo.Claro, _amb.Perfis.Carregar("A").Nativo?.ModoApps);
    }

    [Fact]
    public void Sair_sem_pendencia_nao_pergunta()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        Assert.True(vm.PodeSair());
        Assert.Empty(_dialogos.PerguntasDeSalvar);
    }

    [Fact]
    public void Mensagem_some_depois_de_6s()
    {
        _amb.Perfis.Salvar(Claro("A"));
        var vm = Criar();
        vm.JanelaVisivel = true;
        vm.DescartarCommand.Execute(null);
        Assert.NotNull(vm.Mensagem);
        _relogio.Tique(5);
        Assert.NotNull(vm.Mensagem);
        _relogio.Tique(1);
        Assert.Null(vm.Mensagem);
    }
}

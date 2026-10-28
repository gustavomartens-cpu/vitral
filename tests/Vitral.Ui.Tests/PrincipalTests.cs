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
}

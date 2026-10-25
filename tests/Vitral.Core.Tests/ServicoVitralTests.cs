using Vitral.Core.Aplicacao;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Core.Tests;

public sealed class ServicoVitralTests : IDisposable
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-testes-" + Guid.NewGuid().ToString("N"));
    private readonly RegistroEmMemoria _registro = new();

    public ServicoVitralTests()
    {
        _registro.Gravar(new ValorRegistro(Personalize, "AppsUseLightTheme", TipoValor.Dword, 0u));
        Perfis().Salvar(new Perfil(1, "Claro", null, new AjustesNativosPerfil(null, Modo.Claro, null, null, null, null, null, null)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
    }

    private RepositorioPerfis Perfis() => new(Path.Combine(_raiz, "perfis"));
    private ServicoConfig Config() => new(Path.Combine(_raiz, "config.json"));

    /// Um serviço "novo" sobre as mesmas pastas simula o app aberto de novo.
    private ServicoVitral NovoServico()
    {
        var retratos = new ServicoRetratos(_registro, Path.Combine(_raiz, "retratos"));
        return new ServicoVitral(new Aplicador(_registro, retratos, new NotificadorNulo()), retratos, Perfis(), Config());
    }

    private uint? Apps() => _registro.Ler(Personalize, "AppsUseLightTheme", TipoValor.Dword).Dado as uint?;

    [Fact]
    public void Testar_e_cancelar_volta_ao_anterior()
    {
        var s = NovoServico();
        s.IniciarTeste("Claro");
        Assert.Equal(1u, Apps());
        Assert.NotNull(Config().Ler().TesteEmAndamento);

        Assert.Empty(s.CancelarTeste());

        Assert.Equal(0u, Apps());
        Assert.Null(Config().Ler().TesteEmAndamento);
    }

    [Fact]
    public void Testar_nao_muda_o_perfil_ativo_ate_confirmar()
    {
        var s = NovoServico();
        s.IniciarTeste("Claro");
        Assert.Null(Config().Ler().PerfilAtivo);
        s.ConfirmarTeste();
        Assert.Equal("Claro", Config().Ler().PerfilAtivo);
        Assert.Null(Config().Ler().TesteEmAndamento);
        Assert.Equal(1u, Apps());
    }

    [Fact]
    public void App_morto_no_meio_do_teste_volta_ao_anterior_ao_abrir()
    {
        NovoServico().IniciarTeste("Claro");

        var estado = NovoServico().AoIniciar(inicializacaoDoWindows: false);

        Assert.True(estado.TesteCancelado);
        Assert.Equal(0u, Apps());
        Assert.Null(Config().Ler().TesteEmAndamento);
    }

    [Fact]
    public void Perfil_ativo_sumido_no_login_avisa_sem_aplicar()
    {
        Config().Gravar(ConfigApp.Padrao with { PerfilAtivo = "sumiu" });

        var estado = NovoServico().AoIniciar(inicializacaoDoWindows: true);

        Assert.NotNull(estado.Aviso);
        Assert.Contains("sumiu", estado.Aviso);
        Assert.Equal(0u, Apps());
    }

    [Fact]
    public void Login_reaplica_o_perfil_ativo()
    {
        Config().Gravar(ConfigApp.Padrao with { PerfilAtivo = "Claro" });
        var estado = NovoServico().AoIniciar(inicializacaoDoWindows: true);
        Assert.Null(estado.Aviso);
        Assert.Equal(1u, Apps());
    }

    [Fact]
    public void Abrir_pelo_atalho_nao_reaplica()
    {
        Config().Gravar(ConfigApp.Padrao with { PerfilAtivo = "Claro" });
        NovoServico().AoIniciar(inicializacaoDoWindows: false);
        Assert.Equal(0u, Apps());
    }

    [Fact]
    public void Aplicar_marca_o_perfil_ativo()
    {
        NovoServico().Aplicar("Claro");
        Assert.Equal("Claro", Config().Ler().PerfilAtivo);
    }
}

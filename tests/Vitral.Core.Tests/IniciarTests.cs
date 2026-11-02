using Vitral.Core.Aplicacao;
using Vitral.Core.Configuracao;
using Vitral.Core.Iniciar;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Core.Tests;

internal sealed class ControleIniciarFalso : IControleIniciar
{
    public List<ModoIniciar> Chamadas { get; } = [];
    public void Aplicar(ModoIniciar modo) => Chamadas.Add(modo);
}

public sealed class IniciarTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-iniciar-" + Guid.NewGuid().ToString("N"));
    private readonly ServicoConfig _config;
    private readonly ControleIniciarFalso _controle = new();
    private readonly EstadoIniciar _estado;
    private readonly RepositorioPerfis _perfis;
    private readonly ServicoVitral _servico;

    public IniciarTests()
    {
        var registro = new RegistroEmMemoria();
        _config = new ServicoConfig(Path.Combine(_raiz, "config.json"));
        _estado = new EstadoIniciar(_controle, _config);
        var retratos = new ServicoRetratos(registro, Path.Combine(_raiz, "retratos"), iniciarAtual: _estado.AtualPerfil);
        _perfis = new RepositorioPerfis(Path.Combine(_raiz, "perfis"));
        _servico = new ServicoVitral(new Aplicador(registro, retratos, new NotificadorNulo(), iniciar: _estado), retratos, _perfis, _config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    [Fact]
    public void Perfil_com_iniciar_vai_e_volta()
    {
        var p = new Perfil(4, "Vidro", null, null, Iniciar: new IniciarPerfil(ModoIniciar.Vitral));
        string json = PerfilJson.Escrever(p);
        Assert.Contains("\"iniciar\"", json);
        Assert.Contains("\"modo\": \"vitral\"", json);
        Assert.Equal(p.Iniciar, PerfilJson.Ler(json).Iniciar);
    }

    [Fact]
    public void Modo_desconhecido_vira_nao_mexer()
    {
        var p = PerfilJson.Ler("""{ "esquema": 4, "nome": "Futuro", "iniciar": { "modo": "holografico" } }""");
        Assert.Null(p.Iniciar!.Modo);
    }

    [Fact]
    public void Sem_nada_guardado_e_o_visual_do_Windows()
    {
        Assert.Equal(ModoIniciar.Windows, _estado.Atual());
        _estado.AoIniciar();
        Assert.Equal([ModoIniciar.Windows], _controle.Chamadas);
    }

    [Fact]
    public void Nao_mexer_mantem()
    {
        _estado.Aplicar(new IniciarPerfil(ModoIniciar.Vitral));
        _estado.Aplicar(null);
        _estado.Aplicar(new IniciarPerfil());
        Assert.Equal(ModoIniciar.Vitral, _estado.Atual());
        Assert.Single(_controle.Chamadas);
    }

    [Fact]
    public void Aplicador_aplica_e_reverte_o_iniciar()
    {
        _perfis.Salvar(new Perfil(4, "V", null, null, Iniciar: new IniciarPerfil(ModoIniciar.Vitral)));
        _servico.Aplicar("V");
        Assert.Equal(ModoIniciar.Vitral, _estado.Atual());
        _servico.Desfazer();
        Assert.Equal(ModoIniciar.Windows, _estado.Atual());
        Assert.Equal([ModoIniciar.Vitral, ModoIniciar.Windows], _controle.Chamadas);
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }
}

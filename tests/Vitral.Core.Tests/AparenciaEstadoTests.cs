using Vitral.Core.Aparencia;
using Vitral.Core.Aplicacao;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Core.Tests;

public sealed class AparenciaEstadoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-aparencia-" + Guid.NewGuid().ToString("N"));
    private readonly ServicoConfig _config;
    private readonly ControleAparenciaFalso _controle = new();
    private readonly EstadoAparencia _estado;
    private readonly RepositorioPerfis _perfis;
    private readonly ServicoVitral _servico;

    private static readonly AparenciaPerfil Grafite = new(TemaAparencia.Grafite, Geral: new AjustesAparencia(CorDestaque: "#FF8A8A"));
    private static readonly AparenciaPerfil Claro = new(TemaAparencia.Claro);

    public AparenciaEstadoTests()
    {
        var registro = new RegistroEmMemoria();
        _config = new ServicoConfig(Path.Combine(_raiz, "config.json"));
        _estado = new EstadoAparencia(_controle, _config);
        var retratos = new ServicoRetratos(registro, Path.Combine(_raiz, "retratos"), aparenciaAtual: _estado.Atual);
        _perfis = new RepositorioPerfis(Path.Combine(_raiz, "perfis"));
        _servico = new ServicoVitral(new Aplicador(registro, retratos, new NotificadorNulo(), aparencia: _estado), retratos, _perfis, _config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    [Fact]
    public void Aplicar_grava_e_aplica()
    {
        _estado.Aplicar(Grafite);
        Assert.Equal([Grafite], _controle.Chamadas);
        Assert.Equal(Grafite, _config.Ler().Aparencia);
        Assert.Equal(Grafite, _estado.Atual());
    }

    [Fact]
    public void Nulo_nao_mexe()
    {
        _estado.Aplicar(Grafite);
        _estado.Aplicar(null);
        Assert.Single(_controle.Chamadas);
        Assert.Equal(Grafite, _estado.Atual());
    }

    [Fact]
    public void Sem_guardado_vale_Vidro_escuro()
    {
        Assert.Equal(new AparenciaPerfil(TemaAparencia.VidroEscuro), _estado.Atual());
        _estado.AoIniciar();
        Assert.Equal([new AparenciaPerfil(TemaAparencia.VidroEscuro)], _controle.Chamadas);
    }

    [Fact]
    public void Retrato_guarda_e_devolve_a_aparencia()
    {
        _perfis.Salvar(new Perfil(4, "C", null, null, Aparencia: Claro));
        _perfis.Salvar(new Perfil(4, "G", null, null, Aparencia: Grafite));
        _servico.Aplicar("C");
        _servico.IniciarTeste("G");
        Assert.Equal(Grafite, _estado.Atual());
        _servico.CancelarTeste(); // o Testar (20 s) acabou sem manter
        Assert.Equal(Claro, _estado.Atual());
        _servico.Aplicar("G");
        _servico.Desfazer();
        Assert.Equal(Claro, _estado.Atual());
        _servico.Original();
        Assert.Equal(new AparenciaPerfil(TemaAparencia.VidroEscuro), _estado.Atual());
    }

    [Fact]
    public void Pacote_leva_a_aparencia()
    {
        _perfis.Salvar(new Perfil(4, "G", null, null, Aparencia: Grafite));
        var arquivo = Path.Combine(_raiz, "g.vitral");
        Pacote.Exportar(_perfis, ["G"], arquivo, "0.10.0", "teste");
        var outro = new RepositorioPerfis(Path.Combine(_raiz, "outro"));
        Assert.Equal(Grafite, Pacote.Importar(arquivo, outro).Single().Aparencia);
    }

    private sealed class ControleAparenciaFalso : IControleAparencia
    {
        public List<AparenciaPerfil> Chamadas { get; } = [];
        public void Aplicar(AparenciaPerfil aparencia) => Chamadas.Add(aparencia);
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }
}

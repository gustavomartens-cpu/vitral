using Vitral.Core.Aplicacao;
using Vitral.Core.Barra;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Core.Tests;

public sealed class BarraTopoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-barra-" + Guid.NewGuid().ToString("N"));
    private readonly ControleBarraFalso _controle = new();
    private readonly ServicoConfig _config;
    private readonly ServicoRetratos _retratos;
    private readonly RepositorioPerfis _perfis;
    private readonly ServicoVitral _servico;

    public BarraTopoTests()
    {
        var registro = new RegistroEmMemoria();
        _config = new ServicoConfig(Path.Combine(_raiz, "config.json"));
        var estado = new EstadoBarra(_controle, _config);
        _retratos = new ServicoRetratos(registro, Pasta, estado.Atual);
        _perfis = new RepositorioPerfis(Path.Combine(_raiz, "perfis"));
        _servico = new ServicoVitral(new Aplicador(registro, _retratos, new NotificadorNulo(), estado), _retratos, _perfis, _config);
    }

    private string Pasta => Path.Combine(_raiz, "retratos");

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private void Salvar(string nome, BarraTopoPerfil? barra) =>
        _perfis.Salvar(new Perfil(2, nome, null, null, barra));

    private static readonly BarraTopoPerfil Ligada = new(true);

    [Fact]
    public void Aplicar_perfil_com_barra_liga_e_grava_no_config()
    {
        Salvar("A", Ligada);
        _servico.Aplicar("A");
        Assert.Equal([Ligada], _controle.Chamadas);
        Assert.Equal(Ligada, _config.Ler().BarraTopo);
    }

    [Fact]
    public void Perfil_sem_barra_nao_mexe()
    {
        Salvar("A", null);
        _servico.Aplicar("A");
        Assert.Empty(_controle.Chamadas);
        Assert.Null(_config.Ler().BarraTopo);
    }

    [Fact]
    public void Cancelar_teste_volta_ao_estado_anterior()
    {
        _config.Gravar(_config.Ler() with { BarraTopo = BarraTopoPerfil.Desligada });
        Salvar("A", Ligada);
        _servico.IniciarTeste("A");
        _servico.CancelarTeste();
        Assert.Equal(BarraTopoPerfil.Desligada, _controle.Chamadas[^1]);
        Assert.Equal(BarraTopoPerfil.Desligada, _config.Ler().BarraTopo);
    }

    [Fact]
    public void Desfazer_e_original_levam_a_barra()
    {
        Salvar("A", Ligada);
        _servico.Aplicar("A");
        _servico.Desfazer();
        Assert.Equal(BarraTopoPerfil.Desligada, _controle.Chamadas[^1]);

        _servico.Aplicar("A");
        _servico.Original();
        Assert.Equal(BarraTopoPerfil.Desligada, _controle.Chamadas[^1]);
    }

    [Fact]
    public void Retrato_antigo_sem_barra_nao_mexe()
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Path.Combine(Pasta, "20260101-000000000-000.json"),
            """{ "Quando": "2026-01-01T00:00:00+00:00", "PerfilAplicado": "x", "Valores": [] }""");
        var antigo = _retratos.Ultimo()!;
        Assert.Null(antigo.BarraTopo);

        new Aplicador(new RegistroEmMemoria(), _retratos, new NotificadorNulo(), new EstadoBarra(_controle, _config)).Reverter(antigo);

        Assert.Empty(_controle.Chamadas);
    }

    [Fact]
    public void Original_antigo_sem_barra_vale_desligada()
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Path.Combine(Pasta, "original.json"),
            """{ "Quando": "2026-01-01T00:00:00+00:00", "PerfilAplicado": null, "Valores": [] }""");
        Assert.Equal(BarraTopoPerfil.Desligada, _retratos.GarantirOriginal().BarraTopo);
    }

    [Fact]
    public void Retrato_grava_e_le_barra()
    {
        var barra = new BarraTopoPerfil(true, new IlhasBarra(Midia: false));
        var r = new Retrato(DateTimeOffset.Now, "A", [], barra);
        string arquivo = _retratos.Salvar(r);
        Assert.Equal(barra, _retratos.Carregar(arquivo)!.BarraTopo);
    }

    [Fact]
    public void Config_antigo_sem_barra_le_null()
    {
        Directory.CreateDirectory(_raiz);
        File.WriteAllText(Path.Combine(_raiz, "config.json"), """{ "perfilAtivo": "A", "iniciarComWindows": true }""");
        Assert.Null(_config.Ler().BarraTopo);
        Assert.Equal(BarraTopoPerfil.Desligada, _servico.BarraAtual());
    }

    private sealed class ControleBarraFalso : IControleBarra
    {
        public List<BarraTopoPerfil> Chamadas { get; } = [];
        public void Aplicar(BarraTopoPerfil estado) => Chamadas.Add(estado);
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }
}

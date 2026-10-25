using Vitral.Core.Aplicacao;
using Vitral.Core.Barra;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Core.Tests;

public sealed class DocaTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-doca-" + Guid.NewGuid().ToString("N"));
    private readonly ServicoConfig _config;
    private readonly ControleDocaFalso _controle = new();
    private readonly BarraWindowsFalsa _windows = new();
    private readonly EstadoDoca _doca;
    private readonly ServicoRetratos _retratos;
    private readonly RepositorioPerfis _perfis;
    private readonly ServicoVitral _servico;
    private readonly RegistroEmMemoria _registro = new();
    private readonly NotificadorNulo _notificador = new();

    private static readonly BarraBaixoPerfil Ligada = new(true);

    public DocaTests()
    {
        var registro = _registro;
        var stuck = Enumerable.Repeat((byte)0xAB, 48).ToArray();
        stuck[8] = 0x02;
        registro.Gravar(new ValorRegistro(BarraDeTarefas.StuckRects, "Settings", TipoValor.Binario, stuck));
        _config = new ServicoConfig(Path.Combine(_raiz, "config.json"));
        _doca = new EstadoDoca(_controle, _windows, _config);
        _retratos = new ServicoRetratos(registro, Path.Combine(_raiz, "retratos"), docaAtual: _doca.Atual);
        _perfis = new RepositorioPerfis(Path.Combine(_raiz, "perfis"));
        _servico = new ServicoVitral(new Aplicador(registro, _retratos, _notificador, doca: _doca), _retratos, _perfis, _config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private void Salvar(string nome, BarraBaixoPerfil? doca) => _perfis.Salvar(new Perfil(4, nome, null, null, BarraBaixo: doca));

    [Fact]
    public void Esquema4_ida_e_volta()
    {
        var p = new Perfil(4, "D", null, null, BarraBaixo: Ligada);
        string json = PerfilJson.Escrever(p);
        Assert.Contains("\"esquema\": 4", json);
        Assert.Contains("\"barraBaixo\"", json);
        Assert.Equal(p, PerfilJson.Ler(json));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Esquemas_antigos_leem_barra_baixo_nula(int esquema) =>
        Assert.Null(PerfilJson.Ler($$"""{ "esquema": {{esquema}}, "nome": "A" }""").BarraBaixo);

    [Fact]
    public void Ligar_guarda_o_ocultar_e_esconde_a_barra()
    {
        _doca.Aplicar(Ligada);
        Assert.Equal(["ocultar:True", "esconder"], _windows.Chamadas);
        Assert.Equal([Ligada], _controle.Chamadas);
        Assert.False(_config.Ler().OcultarAutomaticoAntesDaDoca);
        Assert.Equal(Ligada, _config.Ler().BarraBaixo);
    }

    [Fact]
    public void Desligar_mostra_e_restaura_o_ocultar()
    {
        _doca.Aplicar(Ligada);
        _windows.Chamadas.Clear();
        _doca.Aplicar(BarraBaixoPerfil.Desligada);
        Assert.Equal(["mostrar", "ocultar:False"], _windows.Chamadas);
        Assert.Null(_config.Ler().OcultarAutomaticoAntesDaDoca);
        Assert.Equal(BarraBaixoPerfil.Desligada, _controle.Chamadas[^1]);
    }

    [Fact]
    public void Ligar_de_novo_nao_sobrescreve_o_ocultar_guardado()
    {
        _doca.Aplicar(Ligada);
        _doca.Aplicar(Ligada);
        Assert.False(_config.Ler().OcultarAutomaticoAntesDaDoca);
        Assert.Single(_windows.Chamadas, c => c == "ocultar:True");
    }

    [Fact]
    public void Cancelar_teste_desliga_a_doca()
    {
        Salvar("D", Ligada);
        _servico.IniciarTeste("D");
        _servico.CancelarTeste();
        Assert.Equal(BarraBaixoPerfil.Desligada, _doca.Atual());
        Assert.Contains("mostrar", _windows.Chamadas);
        Assert.False(_windows.Ocultar);
    }

    [Fact]
    public void Original_antigo_vale_desligada()
    {
        Directory.CreateDirectory(Path.Combine(_raiz, "retratos"));
        File.WriteAllText(Path.Combine(_raiz, "retratos", "original.json"),
            """{ "Quando": "2026-01-01T00:00:00+00:00", "PerfilAplicado": null, "Valores": [] }""");
        Assert.Equal(BarraBaixoPerfil.Desligada, _retratos.GarantirOriginal().BarraBaixo);
    }

    [Fact]
    public void Ao_iniciar_com_doca_desligada_restaura_a_barra()
    {
        _config.Gravar(_config.Ler() with { BarraBaixo = BarraBaixoPerfil.Desligada, OcultarAutomaticoAntesDaDoca = false });
        _windows.Ocultar = true; // ficou ligado pela doca antes do travamento
        _doca.AoIniciar();
        Assert.Equal(["mostrar", "ocultar:False"], _windows.Chamadas);
        Assert.Null(_config.Ler().OcultarAutomaticoAntesDaDoca);
    }

    [Fact]
    public void Ao_iniciar_com_doca_ligada_esconde()
    {
        _config.Gravar(_config.Ler() with { BarraBaixo = Ligada, OcultarAutomaticoAntesDaDoca = false });
        _doca.AoIniciar();
        Assert.Contains("esconder", _windows.Chamadas);
        Assert.Equal([Ligada], _controle.Chamadas);
    }

    [Fact]
    public void Encerrar_mostra_a_barra_e_mantem_o_config()
    {
        _doca.Aplicar(Ligada);
        _doca.Encerrar();
        Assert.Contains("mostrar", _windows.Chamadas);
        Assert.False(_windows.Ocultar);
        Assert.Equal(Ligada, _config.Ler().BarraBaixo);
        Assert.Null(_config.Ler().OcultarAutomaticoAntesDaDoca);
    }

    private void Salvar(string nome, BarraBaixoPerfil? doca, BarraTarefasPerfil? barra) =>
        _perfis.Salvar(new Perfil(4, nome, null, null, BarraTarefas: barra, BarraBaixo: doca));

    [Fact]
    public void Perfil_com_doca_e_ocultar_desligado_nao_tira_o_ocultar_da_doca()
    {
        _windows.Ocultar = true; // o usuário tinha ocultar automático antes da doca
        Salvar("D", Ligada, new BarraTarefasPerfil(OcultarAutomaticamente: false));

        _servico.Aplicar("D");

        Assert.True(_windows.Ocultar);                                 // a doca precisa do ocultar ligado
        Assert.DoesNotContain(false, _notificador.Ocultar);            // o Explorer não recebe o "desligado" do perfil
        Assert.False(_config.Ler().OcultarAutomaticoAntesDaDoca);      // ele vale quando a doca desligar
    }

    [Fact]
    public void Voltar_com_a_doca_ligada_reafirma_o_ocultar()
    {
        Salvar("D", Ligada);
        _servico.Aplicar("D");
        Salvar("T", null, new BarraTarefasPerfil(OcultarAutomaticamente: false));
        _servico.IniciarTeste("T");
        _servico.CancelarTeste();

        Assert.True(_windows.Ocultar);
        Assert.DoesNotContain(false, _notificador.Ocultar);
    }

    private sealed class ControleDocaFalso : IControleDoca
    {
        public List<BarraBaixoPerfil> Chamadas { get; } = [];
        public void Aplicar(BarraBaixoPerfil estado) => Chamadas.Add(estado);
    }

    private sealed class BarraWindowsFalsa : IBarraDoWindows
    {
        public bool Ocultar { get; set; }
        public List<string> Chamadas { get; } = [];
        public bool? OcultarAutomatico() => Ocultar;
        public void DefinirOcultarAutomatico(bool ligado) { Ocultar = ligado; Chamadas.Add($"ocultar:{ligado}"); }
        public void Esconder() => Chamadas.Add("esconder");
        public void Mostrar() => Chamadas.Add("mostrar");
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public List<bool> Ocultar { get; } = [];
        public void AvisarOcultarAutomatico(bool ligado) => Ocultar.Add(ligado);
    }
}


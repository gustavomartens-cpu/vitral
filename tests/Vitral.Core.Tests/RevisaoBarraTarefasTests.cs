using Vitral.Core.Aplicacao;
using Vitral.Core.Barra;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Core.Tests;

/// Correções da revisão final da fase da barra de tarefas (relógio e lista de ícones).
public sealed class RevisaoBarraTarefasTests : IDisposable
{
    private const string Advanced = BarraDeTarefas.Advanced;
    private const string Relogio = BarraDeTarefas.NomeRelogio;

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-rbt-" + Guid.NewGuid().ToString("N"));
    private readonly RegistroEmMemoria _registro = new();
    private readonly ServicoConfig _config;
    private readonly ControleFalso _controle = new();
    private readonly EstadoBarra _barra;
    private readonly EstadoIcones _icones;
    private readonly ServicoRetratos _retratos;
    private readonly RepositorioPerfis _perfis;

    public RevisaoBarraTarefasTests()
    {
        _config = new ServicoConfig(Path.Combine(_raiz, "config.json"));
        _barra = new EstadoBarra(_controle, _config, _registro, new NotificadorNulo());
        _icones = new EstadoIcones(_config);
        _retratos = new ServicoRetratos(_registro, Path.Combine(_raiz, "retratos"), _barra.Atual, _icones.Atual);
        _perfis = new RepositorioPerfis(Path.Combine(_raiz, "perfis"));
        Icone("1", "Spotify.exe");
        Icone("2", "steam.exe");
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private void Icone(string id, string exe) =>
        _registro.Gravar(new ValorRegistro($@"{Icones.Chave}\{id}", "ExecutablePath", TipoValor.Texto, $@"C:\{exe}"));

    private Aplicador Aplicador(IRegistro? registro = null) =>
        new(registro ?? _registro, _retratos, new NotificadorNulo(), _barra, _icones);

    private ServicoVitral Servico(IRegistro? registro = null) => new(Aplicador(registro), _retratos, _perfis, _config);

    private uint? ValorDoRelogio() => _registro.Ler(Advanced, Relogio, TipoValor.Dword).Dado as uint?;

    private void Salvar(string nome, BarraTopoPerfil? topo = null, BarraTarefasPerfil? tarefas = null) =>
        _perfis.Salvar(new Perfil(3, nome, null, null, topo, tarefas));

    // ---- relógio: segue a barra do topo de verdade (I1, I2, I3) ----

    [Fact]
    public void Relogio_segue_a_barra_do_topo()
    {
        Aplicador().Aplicar(new Perfil(3, "A", null, null, new BarraTopoPerfil(true)));
        Assert.Equal(0u, ValorDoRelogio());
        Aplicador().Aplicar(new Perfil(3, "B", null, null, BarraTopoPerfil.Desligada));
        Assert.Null(ValorDoRelogio());

        _registro.Gravar(new ValorRegistro(Advanced, Relogio, TipoValor.Dword, 0u));
        Aplicador().Aplicar(new Perfil(3, "C", null, null)); // perfil que não fala da barra do topo
        Assert.Equal(0u, ValorDoRelogio());
    }

    [Fact]
    public void Sem_barra_do_topo_o_aplicador_nao_toca_o_relogio()
    {
        var cli = new Aplicador(_registro, _retratos, new NotificadorNulo()); // como a linha de comando
        cli.Aplicar(new Perfil(3, "A", null, null, new BarraTopoPerfil(true)));
        Assert.Null(ValorDoRelogio());
    }

    [Fact]
    public void Encerrar_devolve_o_relogio_e_mostrar_esconde_de_novo()
    {
        _barra.Aplicar(new BarraTopoPerfil(true));
        _barra.Encerrar();
        Assert.Null(ValorDoRelogio());

        _barra.Mostrar();
        Assert.Equal(0u, ValorDoRelogio());
        Assert.Equal(new BarraTopoPerfil(true), _controle.Chamadas[^1]);
    }

    [Fact]
    public void Original_antigo_devolve_o_relogio()
    {
        Directory.CreateDirectory(Path.Combine(_raiz, "retratos"));
        File.WriteAllText(Path.Combine(_raiz, "retratos", "original.json"),
            """{ "Quando": "2026-01-01T00:00:00+00:00", "PerfilAplicado": null, "Valores": [] }""");
        Salvar("A", topo: new BarraTopoPerfil(true));
        var servico = Servico();
        servico.Aplicar("A");
        servico.Original();
        Assert.Null(ValorDoRelogio());
    }

    // ---- lista de ícones: vai no retrato e volta com ele (I4, I5, I6) ----

    [Fact]
    public void Voltar_de_um_teste_restaura_a_lista_do_perfil_ativo()
    {
        Salvar("A", tarefas: new BarraTarefasPerfil(IconesVisiveis: ["steam.exe"]));
        Salvar("B", tarefas: new BarraTarefasPerfil(IconesVisiveis: ["Spotify.exe"]));
        var servico = Servico();
        servico.Aplicar("A");
        servico.IniciarTeste("B");
        Assert.Equal(["Spotify.exe"], _icones.Atual()!);
        servico.CancelarTeste();
        Assert.Equal(["steam.exe"], _icones.Atual()!);
    }

    [Fact]
    public void Perfil_que_nao_mexe_nos_icones_limpa_a_lista()
    {
        Salvar("A", tarefas: new BarraTarefasPerfil(IconesVisiveis: ["steam.exe"]));
        Salvar("Jogo", tarefas: new BarraTarefasPerfil(Selos: false));
        var servico = Servico();
        servico.Aplicar("A");
        servico.Aplicar("Jogo");
        Assert.Null(_icones.Atual());
    }

    [Fact]
    public void Lista_volta_antes_de_regravar_os_icones()
    {
        Salvar("A", tarefas: new BarraTarefasPerfil(IconesVisiveis: ["steam.exe"]));
        var espiao = new RegistroEspiao(_registro, _icones);
        var servico = Servico(espiao);
        servico.IniciarTeste("A");
        espiao.Espiar = true;
        servico.CancelarTeste();
        Assert.NotEmpty(espiao.ListasVistas);
        Assert.All(espiao.ListasVistas, l => Assert.Null(l)); // o observador já via "não mexer" enquanto o Voltar regravava
    }

    private sealed class RegistroEspiao(RegistroEmMemoria interno, EstadoIcones icones) : IRegistro
    {
        public bool Espiar { get; set; }
        public List<IReadOnlyList<string>?> ListasVistas { get; } = [];

        public ValorRegistro Ler(string chave, string nome, TipoValor tipo) => interno.Ler(chave, nome, tipo);
        public IReadOnlyList<string> SubChaves(string chave) => interno.SubChaves(chave);

        public void Gravar(ValorRegistro v) { Ver(v.Chave); interno.Gravar(v); }
        public void Apagar(string chave, string nome) { Ver(chave); interno.Apagar(chave, nome); }

        private void Ver(string chave)
        {
            if (Espiar && chave.StartsWith(Icones.Chave, StringComparison.OrdinalIgnoreCase)) ListasVistas.Add(icones.Atual());
        }
    }

    private sealed class ControleFalso : IControleBarra
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

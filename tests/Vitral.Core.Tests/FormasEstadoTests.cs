using Vitral.Core.Aplicacao;
using Vitral.Core.Configuracao;
using Vitral.Core.Formas;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Core.Tests;

public sealed class FormasEstadoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-formas-" + Guid.NewGuid().ToString("N"));
    private readonly ServicoConfig _config;
    private readonly ControleFormasFalso _controle = new();
    private readonly EstadoFormas _estado;
    private readonly RepositorioPerfis _perfis;
    private readonly ServicoVitral _servico;

    private static readonly FormasPerfil Corrida = new(new FormasDoca(FormatoDoca.Corrida, TamanhoIcones.Grande, false));
    private static readonly FormasPerfil Ilha = new(Notch: new FormasNotch(FormatoNotch.Ilha), Ilhas: new FormasIlhas(DisposicaoIlhas.BarraUnica, DistanciaIlhas.Encostadas));

    public FormasEstadoTests()
    {
        var registro = new RegistroEmMemoria();
        _config = new ServicoConfig(Path.Combine(_raiz, "config.json"));
        _estado = new EstadoFormas(_controle, _config);
        var retratos = new ServicoRetratos(registro, Path.Combine(_raiz, "retratos"), formasAtuais: _estado.Atual);
        _perfis = new RepositorioPerfis(Path.Combine(_raiz, "perfis"));
        _servico = new ServicoVitral(new Aplicador(registro, retratos, new NotificadorNulo(), formas: _estado), retratos, _perfis, _config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    [Fact]
    public void Aplicar_grava_e_avisa()
    {
        _estado.Aplicar(Corrida);
        Assert.Equal([Corrida], _controle.Chamadas);
        Assert.Equal(Corrida, _config.Ler().Formas);
        Assert.Equal(Corrida, _estado.Atual());
    }

    [Fact]
    public void Nulo_nao_mexe()
    {
        _estado.Aplicar(Corrida);
        _estado.Aplicar(null);
        Assert.Single(_controle.Chamadas);
        Assert.Equal(Corrida, _estado.Atual());
    }

    [Fact]
    public void AoIniciar_aplica_o_guardado()
    {
        Assert.Equal(EstadoFormas.Padrao, _estado.Atual());
        _estado.AoIniciar();
        Assert.Equal([EstadoFormas.Padrao], _controle.Chamadas);
        _estado.Aplicar(Ilha);
        _estado.AoIniciar();
        Assert.Equal(Ilha, _controle.Chamadas[^1]);
    }

    [Fact]
    public void Perfil_sem_formas_nao_mexe()
    {
        _estado.Aplicar(Corrida);
        _perfis.Salvar(new Perfil(4, "Velho", null, null));
        _servico.Aplicar("Velho");
        Assert.Single(_controle.Chamadas);
        Assert.Equal(Corrida, _estado.Atual());
    }

    [Fact]
    public void Perfil_com_formas_ida_e_volta_no_pacote()
    {
        _perfis.Salvar(new Perfil(4, "I", null, null, Formas: Ilha));
        var arquivo = Path.Combine(_raiz, "i.vitral");
        Pacote.Exportar(_perfis, ["I"], arquivo, "0.11.0", "teste");
        var outro = new RepositorioPerfis(Path.Combine(_raiz, "outro"));
        Assert.Equal(Ilha, Pacote.Importar(arquivo, outro).Single().Formas);
        Assert.Contains("\"ilha\"", File.ReadAllText(Directory.GetFiles(Path.Combine(_raiz, "perfis"), "*.json", SearchOption.AllDirectories).Single()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retrato_e_reverter_devolvem_as_formas()
    {
        _perfis.Salvar(new Perfil(4, "C", null, null, Formas: Corrida));
        _perfis.Salvar(new Perfil(4, "I", null, null, Formas: Ilha));
        _servico.Aplicar("C");
        _servico.IniciarTeste("I");
        Assert.Equal(new FormasPerfil(Corrida.Doca, Ilha.Notch, Ilha.Ilhas), _estado.Atual()); // o que I não escolheu fica
        _servico.CancelarTeste();
        Assert.Equal(Corrida, _estado.Atual());
        _servico.Aplicar("I");
        _servico.Desfazer();
        Assert.Equal(Corrida, _estado.Atual());
    }

    [Fact]
    public void Original_sem_formas_volta_ao_padrao()
    {
        _perfis.Salvar(new Perfil(4, "C", null, null, Formas: Corrida));
        _servico.Aplicar("C");
        _servico.Original();
        Assert.Equal(EstadoFormas.Padrao, _estado.Atual());
    }

    // Revisão final, Important 1 (decisão do Gustavo, opção a): "Não mexer" num campo mantém o que está em uso.
    [Fact]
    public void Nao_mexer_por_campo_mantem_o_que_esta()
    {
        _perfis.Salvar(new Perfil(4, "C", null, null, Formas: Corrida));
        _perfis.Salvar(new Perfil(4, "N", null, null, Formas: new FormasPerfil(Notch: new FormasNotch(FormatoNotch.Ilha))));
        _servico.Aplicar("C");
        _servico.Aplicar("N");
        Assert.Equal(new FormasPerfil(Corrida.Doca, new FormasNotch(FormatoNotch.Ilha), null), _estado.Atual());
        // Campo a campo, inclusive dentro da parte: só os ícones mudam, o formato corrida fica.
        _estado.Aplicar(new FormasPerfil(new FormasDoca(Icones: TamanhoIcones.Pequeno)));
        Assert.Equal(new FormasDoca(FormatoDoca.Corrida, TamanhoIcones.Pequeno, false), _estado.Atual().Doca);
    }

    [Fact]
    public void Desfazer_volta_campo_que_estava_em_nao_mexer()
    {
        _perfis.Salvar(new Perfil(4, "C", null, null, Formas: Corrida));
        _perfis.Salvar(new Perfil(4, "N", null, null, Formas: new FormasPerfil(Notch: new FormasNotch(FormatoNotch.Ilha))));
        _servico.Aplicar("N");
        _servico.IniciarTeste("C");
        _servico.CancelarTeste();
        Assert.Equal(new FormasPerfil(Notch: new FormasNotch(FormatoNotch.Ilha)), _estado.Atual());
    }

    // Revisão final, minor 2: formas (e aparência) antes da barra e da doca, para elas nascerem com a forma certa.
    [Fact]
    public void Formas_aplicadas_antes_da_barra()
    {
        var ordem = new List<string>();
        var registro = new RegistroEmMemoria();
        var estadoFormas = new EstadoFormas(new ControleQueAnota(ordem, "formas"), _config);
        var estadoBarra = new Vitral.Core.Barra.EstadoBarra(new ControleQueAnota(ordem, "barra"), _config);
        var retratos = new ServicoRetratos(registro, Path.Combine(_raiz, "retratos2"), formasAtuais: estadoFormas.Atual);
        var aplicador = new Aplicador(registro, retratos, new NotificadorNulo(), estadoBarra, formas: estadoFormas);
        var p = new Perfil(4, "B", null, null, BarraTopo: new BarraTopoPerfil(true), Formas: Corrida);
        aplicador.Aplicar(p);
        Assert.Equal(["formas", "barra"], ordem);
        ordem.Clear();
        aplicador.Reverter(retratos.Tirar(null) with { BarraTopo = new BarraTopoPerfil(true), Formas = Corrida });
        Assert.Equal(["formas", "barra"], ordem);
    }

    private sealed class ControleQueAnota(List<string> ordem, string nome) : IControleFormas, Vitral.Core.Barra.IControleBarra
    {
        public void Aplicar(FormasPerfil formas) => ordem.Add(nome);
        public void Aplicar(BarraTopoPerfil estado) => ordem.Add(nome);
    }

    private sealed class ControleFormasFalso : IControleFormas
    {
        public List<FormasPerfil> Chamadas { get; } = [];
        public void Aplicar(FormasPerfil formas) => Chamadas.Add(formas);
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }
}

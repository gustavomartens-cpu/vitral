using Vitral.Core.Configuracao;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Tests;

public sealed class ConfigTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "vitral-testes-" + Guid.NewGuid().ToString("N"));
    private string Arquivo => Path.Combine(_pasta, "config.json");

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public void Ler_sem_arquivo_devolve_padrao()
    {
        Assert.Equal(ConfigApp.Padrao, new ServicoConfig(Arquivo).Ler());
    }

    [Fact]
    public void Atualizar_de_varias_threads_nao_perde_nada()
    {
        // O modo avião grava o config fora da thread da interface (revisão dos controles, I4).
        var servico = new ServicoConfig(Arquivo);
        Parallel.For(0, 40, i => new ServicoConfig(Arquivo).Atualizar(c =>
            c with { RadiosAntesDoAviao = new Dictionary<string, bool>(c.RadiosAntesDoAviao ?? new Dictionary<string, bool>()) { [$"r{i}"] = true } }));
        Assert.Equal(40, servico.Ler().RadiosAntesDoAviao!.Count);
    }

    [Fact]
    public void Gravar_e_ler_preserva_tudo()
    {
        var servico = new ServicoConfig(Arquivo);
        var c = new ConfigApp("Vidro escuro", false, new TesteEmAndamento("Vermelho", "20261024-205240586-000.json"));
        servico.Gravar(c);
        Assert.Equal(c, servico.Ler());
    }

    [Fact]
    public void Arquivo_corrompido_devolve_padrao()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Arquivo, "{lixo");
        Assert.Equal(ConfigApp.Padrao, new ServicoConfig(Arquivo).Ler());
    }

    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private sealed class TarefaEmMemoria(bool aceita = true) : ITarefaDeLogin
    {
        public (string Exe, string Args)? Criada { get; private set; }
        public bool Existe() => Criada is not null;
        public bool Criar(string caminhoExe, string argumentos)
        {
            if (aceita) Criada = (caminhoExe, argumentos);
            return aceita;
        }
        public void Apagar() => Criada = null;
    }

    private static string? ValorNoRun(RegistroEmMemoria registro) => registro.Ler(Run, "Vitral", TipoValor.Texto).Dado as string;

    [Fact]
    public void Ativar_inicializacao_cria_a_tarefa_e_tira_o_Run()
    {
        var registro = new RegistroEmMemoria();
        registro.Gravar(new ValorRegistro(Run, "Vitral", TipoValor.Texto, "\"C:\\x\\Vitral.exe\" --inicializacao"));
        var tarefa = new TarefaEmMemoria();
        var ini = new Inicializacao(registro, tarefa);
        ini.Ativar(@"C:\x\Vitral.exe");
        Assert.Equal((@"C:\x\Vitral.exe", "--inicializacao"), tarefa.Criada);
        Assert.Null(ValorNoRun(registro));
        Assert.True(ini.Ativa());
    }

    [Fact]
    public void Tarefa_recusada_cai_no_Run()
    {
        var registro = new RegistroEmMemoria();
        var ini = new Inicializacao(registro, new TarefaEmMemoria(aceita: false));
        ini.Ativar(@"C:\x\Vitral.exe");
        Assert.Equal("\"C:\\x\\Vitral.exe\" --inicializacao", ValorNoRun(registro));
        Assert.True(ini.Ativa());
    }

    [Fact]
    public void Desativar_inicializacao_apaga_tarefa_e_Run()
    {
        var registro = new RegistroEmMemoria();
        registro.Gravar(new ValorRegistro(Run, "Vitral", TipoValor.Texto, "\"C:\\x\\Vitral.exe\" --inicializacao"));
        var ini = new Inicializacao(registro, new TarefaEmMemoria());
        ini.Ativar(@"C:\x\Vitral.exe");
        ini.Desativar();
        Assert.False(ini.Ativa());
        Assert.Null(ValorNoRun(registro));
    }

    [Fact]
    public void Migrar_passa_o_exe_do_Run_para_a_tarefa()
    {
        var registro = new RegistroEmMemoria();
        registro.Gravar(new ValorRegistro(Run, "Vitral", TipoValor.Texto, "\"C:\\Users\\a b\\VitralApp\\current\\Vitral.exe\" --inicializacao"));
        var tarefa = new TarefaEmMemoria();
        new Inicializacao(registro, tarefa).Migrar();
        Assert.Equal((@"C:\Users\a b\VitralApp\current\Vitral.exe", "--inicializacao"), tarefa.Criada);
        Assert.Null(ValorNoRun(registro));
    }

    [Fact]
    public void Migrar_sem_Run_nao_liga_nada()
    {
        var tarefa = new TarefaEmMemoria();
        new Inicializacao(new RegistroEmMemoria(), tarefa).Migrar();
        Assert.Null(tarefa.Criada);
    }

    [Fact]
    public void Salvar_retrato_devolve_nome_que_Carregar_le()
    {
        var servico = new ServicoRetratos(new RegistroEmMemoria(), _pasta);
        var r = servico.Tirar("p");
        string nome = servico.Salvar(r);
        var lido = servico.Carregar(nome);
        Assert.NotNull(lido);
        Assert.Equal(r.Valores, lido!.Valores);
        Assert.Equal("p", lido.PerfilAplicado);
    }

    [Fact]
    public void Carregar_retrato_inexistente_devolve_nulo()
    {
        Assert.Null(new ServicoRetratos(new RegistroEmMemoria(), _pasta).Carregar("nao-existe.json"));
    }
}


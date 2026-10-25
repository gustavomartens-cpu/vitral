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

    [Fact]
    public void Ativar_inicializacao_grava_o_comando_no_Run()
    {
        var registro = new RegistroEmMemoria();
        var ini = new Inicializacao(registro);
        ini.Ativar(@"C:\x\Vitral.exe");
        var v = registro.Ler(@"Software\Microsoft\Windows\CurrentVersion\Run", "Vitral", TipoValor.Texto);
        Assert.Equal("\"C:\\x\\Vitral.exe\" --inicializacao", v.Dado);
        Assert.True(ini.Ativa());
    }

    [Fact]
    public void Desativar_inicializacao_apaga_o_valor()
    {
        var registro = new RegistroEmMemoria();
        var ini = new Inicializacao(registro);
        ini.Ativar(@"C:\x\Vitral.exe");
        ini.Desativar();
        Assert.False(ini.Ativa());
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

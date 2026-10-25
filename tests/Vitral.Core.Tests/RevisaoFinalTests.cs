using System.IO.Compression;
using Vitral.Cli;
using Vitral.Core.Aplicacao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Tests;

/// Testes dos achados Importantes da revisão final da fase 1a.
public sealed class RevisaoFinalTests : IDisposable
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "vitral-testes-" + Guid.NewGuid().ToString("N"));
    private readonly RegistroEmMemoria _registro = new();

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }

    private (Aplicador, ServicoRetratos) Criar()
    {
        var retratos = new ServicoRetratos(_registro, Path.Combine(_raiz, "retratos"));
        return (new Aplicador(_registro, retratos, new NotificadorNulo()), retratos);
    }

    private static Perfil PerfilApps(Modo m) => new(1, "p", null, new AjustesNativosPerfil(null, m, null, null, null, null, null, null));

    // Importante 1: Ctrl+C e teto de segundos

    [Fact]
    public void Esperar_cancelado_volta_na_hora_sem_confirmar()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(Confirmacao.Esperar(() => { Thread.Sleep(5000); return "s"; }, TimeSpan.FromSeconds(10), cts.Token));
        Assert.True(relogio.ElapsedMilliseconds < 2000);
    }

    [Fact]
    public void Segundos_acima_do_teto_e_uso()
    {
        Assert.IsType<Comando.Uso>(Comando.Interpretar(["testar", "x", "--segundos", "601"]));
        Assert.Equal(new Comando.Testar("x", 600), Comando.Interpretar(["testar", "x", "--segundos", "600"]));
    }

    // Importante 2: desfazer/original guardam retrato antes de agir

    [Fact]
    public void Restaurar_guarda_o_estado_atual_antes_e_permite_voltar()
    {
        var (aplicador, retratos) = Criar();
        _registro.Gravar(new ValorRegistro(Personalize, "AppsUseLightTheme", TipoValor.Dword, 0u));
        var original = retratos.GarantirOriginal();
        _registro.Gravar(new ValorRegistro(Personalize, "AppsUseLightTheme", TipoValor.Dword, 1u)); // personalização do usuário

        aplicador.Restaurar(original, "original");
        Assert.Equal(0u, _registro.Ler(Personalize, "AppsUseLightTheme", TipoValor.Dword).Dado);

        aplicador.Restaurar(retratos.Ultimo()!, "desfazer");
        Assert.Equal(1u, _registro.Ler(Personalize, "AppsUseLightTheme", TipoValor.Dword).Dado);
    }

    // Importante 3: reversão relata o que não voltou

    [Fact]
    public void Reverter_lista_o_que_foi_bloqueado()
    {
        var (aplicador, _) = Criar();
        var res = aplicador.Aplicar(PerfilApps(Modo.Claro));
        _registro.Bloqueados.Add((Personalize, "AppsUseLightTheme"));

        var naoVoltou = aplicador.Reverter(res.RetratoAnterior);

        var item = Assert.Single(naoVoltou);
        Assert.Equal("AppsUseLightTheme", item.Valor.Nome);
    }

    // Importante 4: importar não sobrescreve sem pedir

    [Fact]
    public void Importar_recusa_perfil_que_ja_existe_sem_substituir()
    {
        var a = new RepositorioPerfis(Path.Combine(_raiz, "A"));
        var b = new RepositorioPerfis(Path.Combine(_raiz, "B"));
        a.Salvar(PerfilApps(Modo.Claro));
        b.Salvar(PerfilApps(Modo.Escuro));
        string zip = Path.Combine(_raiz, "p.vitral");
        Pacote.Exportar(a, ["p"], zip, "0.1.0", "x");

        var ex = Assert.Throws<PerfilJaExisteException>(() => Pacote.Importar(zip, b));
        Assert.Contains("já existe", ex.Message);
        Assert.Equal(["p"], ex.Nomes);
        Assert.Equal(Modo.Escuro, b.Carregar("p").Nativo!.ModoApps);

        Pacote.Importar(zip, b, substituir: true);
        Assert.Equal(Modo.Claro, b.Carregar("p").Nativo!.ModoApps);
    }

    [Fact]
    public void Importar_com_substituir_e_interpretado()
    {
        Assert.Equal(new Comando.Importar("x.vitral", true), Comando.Interpretar(["importar", "x.vitral", "--substituir"]));
        Assert.Equal(new Comando.Importar("x.vitral", false), Comando.Interpretar(["importar", "x.vitral"]));
    }

    // Importante 5: arquivo que não é zip vira mensagem clara

    [Fact]
    public void Importar_arquivo_que_nao_e_zip_e_perfil_invalido()
    {
        Directory.CreateDirectory(_raiz);
        string falso = Path.Combine(_raiz, "falso.vitral");
        File.WriteAllText(falso, "isto não é um zip");
        var ex = Assert.Throws<PerfilInvalidoException>(() => Pacote.Importar(falso, new RepositorioPerfis(Path.Combine(_raiz, "B"))));
        Assert.Contains(".vitral", ex.Message);
    }

    // Importante 6: esquema futuro é reconhecido antes do resto

    [Fact]
    public void Esquema_futuro_com_campo_desconhecido_cita_o_esquema()
    {
        var ex = Assert.Throws<PerfilInvalidoException>(() =>
            PerfilJson.Ler("""{ "esquema": 5, "nome": "x", "nativo": { "modoSistema": "auto" } }"""));
        Assert.Contains("esquema 5", ex.Message);
    }
}

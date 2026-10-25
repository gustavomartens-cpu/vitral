using Vitral.Core.Aplicacao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Tests;

public sealed class AplicadorTests : IDisposable
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string Dwm = @"Software\Microsoft\Windows\DWM";
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "vitral-testes-" + Guid.NewGuid().ToString("N"));
    private readonly RegistroEmMemoria _registro = new();
    private readonly NotificadorFalso _notificador = new();

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private Aplicador Criar(IRegistro? registro = null)
    {
        var r = registro ?? _registro;
        return new Aplicador(r, new ServicoRetratos(r, _pasta), _notificador);
    }

    private static Perfil Perfil(AjustesNativosPerfil a) => new(1, "teste", null, a);
    private static AjustesNativosPerfil Vazio() => new(null, null, null, null, null, null, null, null);

    [Fact]
    public void Aplicar_grava_os_valores_e_avisa_o_tema_uma_vez()
    {
        var res = Criar().Aplicar(Perfil(Vazio() with { CorDestaque = "#21375E" }));
        Assert.True(res.Ok);
        Assert.Equal(0xFF5E3721u, _registro.Ler(Dwm, "AccentColor", TipoValor.Dword).Dado);
        Assert.Equal(1, _notificador.Tema);
        Assert.Equal(0, _notificador.Barra);
    }

    [Fact]
    public void Reverter_apaga_o_valor_que_nao_existia_antes()
    {
        var aplicador = Criar();
        var res = aplicador.Aplicar(Perfil(Vazio() with { AlinhamentoBarra = Alinhamento.Esquerda }));
        Assert.Equal(0u, _registro.Ler(Advanced, "TaskbarAl", TipoValor.Dword).Dado);

        aplicador.Reverter(res.RetratoAnterior);

        Assert.True(_registro.Ler(Advanced, "TaskbarAl", TipoValor.Dword).Ausente);
    }

    [Fact]
    public void Reverter_restaura_o_valor_que_existia()
    {
        _registro.Gravar(new ValorRegistro(Personalize, "AppsUseLightTheme", TipoValor.Dword, 0u));
        var aplicador = Criar();
        var res = aplicador.Aplicar(Perfil(Vazio() with { ModoApps = Modo.Claro }));
        aplicador.Reverter(res.RetratoAnterior);
        Assert.Equal(0u, _registro.Ler(Personalize, "AppsUseLightTheme", TipoValor.Dword).Dado);
    }

    [Fact]
    public void Escrita_bloqueada_aplica_o_resto_e_lista_o_item()
    {
        _registro.Bloqueados.Add((Personalize, "ColorPrevalence"));
        var res = Criar().Aplicar(Perfil(Vazio() with
        {
            ModoSistema = Modo.Escuro, DestaqueNaBarraEIniciar = true, Transparencia = true,
        }));

        Assert.False(res.Ok);
        var item = Assert.Single(res.NaoAplicados);
        Assert.Equal("ColorPrevalence", item.Valor.Nome);
        Assert.Equal(0u, _registro.Ler(Personalize, "SystemUsesLightTheme", TipoValor.Dword).Dado);
        Assert.Equal(1u, _registro.Ler(Personalize, "EnableTransparency", TipoValor.Dword).Dado);
    }

    [Fact]
    public void Falha_inesperada_desfaz_o_que_ja_foi_gravado_e_relanca()
    {
        _registro.Gravar(new ValorRegistro(Personalize, "SystemUsesLightTheme", TipoValor.Dword, 1u));
        var instavel = new RegistroQueFalhaNaGravacao(_registro, falharNa: 3);
        var aplicador = Criar(instavel);

        Assert.Throws<IOException>(() => aplicador.Aplicar(Perfil(Vazio() with
        {
            ModoSistema = Modo.Escuro, ModoApps = Modo.Escuro, Transparencia = true, DestaqueNosTitulos = true,
        })));

        Assert.Equal(1u, _registro.Ler(Personalize, "SystemUsesLightTheme", TipoValor.Dword).Dado);
        Assert.True(_registro.Ler(Personalize, "AppsUseLightTheme", TipoValor.Dword).Ausente);
    }

    [Fact]
    public void Aviso_da_barra_so_quando_o_alinhamento_muda()
    {
        Criar().Aplicar(Perfil(Vazio() with { AlinhamentoBarra = Alinhamento.Centro }));
        Assert.Equal(1, _notificador.Barra);
    }

    [Fact]
    public void Aplicar_guarda_o_original_e_o_retrato_anterior()
    {
        var servico = new ServicoRetratos(_registro, _pasta);
        new Aplicador(_registro, servico, _notificador).Aplicar(Perfil(Vazio() with { ModoApps = Modo.Claro }));
        Assert.True(File.Exists(Path.Combine(_pasta, "original.json")));
        Assert.Single(servico.Historico());
    }

    private sealed class NotificadorFalso : INotificador
    {
        public int Tema { get; private set; }
        public int Barra { get; private set; }
        public void AvisarMudancaDeTema() => Tema++;
        public void AvisarMudancaDaBarra() => Barra++;
        public void AvisarOcultarAutomatico(bool ligado) { }
    }

    /// Delega ao registro real em memória, mas lança IOException na N-ésima gravação.
    private sealed class RegistroQueFalhaNaGravacao(RegistroEmMemoria interno, int falharNa) : IRegistro
    {
        private int _gravacoes;
        public ValorRegistro Ler(string chave, string nome, TipoValor tipo) => interno.Ler(chave, nome, tipo);
        public void Apagar(string chave, string nome) => interno.Apagar(chave, nome);
        public void Gravar(ValorRegistro v)
        {
            if (++_gravacoes == falharNa) throw new IOException("falha simulada");
            interno.Gravar(v);
        }
    }
}

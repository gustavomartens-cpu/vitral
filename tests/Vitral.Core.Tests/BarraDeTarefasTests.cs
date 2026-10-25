using Vitral.Core.Aplicacao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Tests;

public sealed class BarraDeTarefasTests : IDisposable
{
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string Search = @"Software\Microsoft\Windows\CurrentVersion\Search";
    private const string Stuck = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";

    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "vitral-bt-" + Guid.NewGuid().ToString("N"));
    private readonly RegistroEmMemoria _registro = new();
    private readonly NotificadorGravador _notificador = new();
    private readonly ServicoRetratos _retratos;
    private readonly Aplicador _aplicador;

    public BarraDeTarefasTests()
    {
        _retratos = new ServicoRetratos(_registro, _pasta);
        _aplicador = new Aplicador(_registro, _retratos, _notificador);
        _registro.Gravar(new ValorRegistro(Stuck, "Settings", TipoValor.Binario, StuckOriginal()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private static byte[] StuckOriginal()
    {
        var b = Enumerable.Repeat((byte)0xAB, 48).ToArray();
        b[8] = 0x02;
        return b;
    }

    private static Perfil P(BarraTarefasPerfil? b = null, BarraTopoPerfil? topo = null) => new(3, "T", null, null, topo, b);

    private uint? Dword(string chave, string nome) => _registro.Ler(chave, nome, TipoValor.Dword).Dado as uint?;
    private byte[]? Bytes() => _registro.Ler(Stuck, "Settings", TipoValor.Binario).Dado as byte[];

    [Theory]
    [InlineData(PesquisaBarra.Oculta, 0u)]
    [InlineData(PesquisaBarra.Icone, 1u)]
    [InlineData(PesquisaBarra.Caixa, 2u)]
    public void Pesquisa_vira_o_valor_da_spec(PesquisaBarra pesquisa, uint esperado)
    {
        _aplicador.Aplicar(P(new BarraTarefasPerfil(Pesquisa: pesquisa)));
        Assert.Equal(esperado, Dword(Search, "SearchboxTaskbarMode"));
    }

    [Theory]
    [InlineData(AgruparBotoes.Sempre, 0u)]
    [InlineData(AgruparBotoes.QuandoCheia, 1u)]
    [InlineData(AgruparBotoes.Nunca, 2u)]
    public void Agrupar_vira_o_valor_da_spec(AgruparBotoes agrupar, uint esperado)
    {
        _aplicador.Aplicar(P(new BarraTarefasPerfil(Agrupar: agrupar)));
        Assert.Equal(esperado, Dword(Advanced, "TaskbarGlomLevel"));
    }

    [Fact]
    public void Opcoes_simples_viram_0_ou_1()
    {
        _aplicador.Aplicar(P(new BarraTarefasPerfil(VisaoTarefas: true, MostrarAreaDeTrabalho: false, TodosOsMonitores: true,
            Selos: false, Piscar: true)));
        Assert.Equal(1u, Dword(Advanced, "ShowTaskViewButton"));
        Assert.Equal(0u, Dword(Advanced, "TaskbarSd"));
        Assert.Equal(1u, Dword(Advanced, "MMTaskbarEnabled"));
        Assert.Equal(0u, Dword(Advanced, "TaskbarBadges"));
        Assert.Equal(1u, Dword(Advanced, "TaskbarFlashing"));
    }

    [Fact]
    public void Ocultar_automatico_muda_so_o_bit_do_byte_8()
    {
        _aplicador.Aplicar(P(new BarraTarefasPerfil(OcultarAutomaticamente: true)));
        var b = Bytes()!;
        Assert.Equal(0x03, b[8]);
        Assert.All(b.Where((_, i) => i != 8), x => Assert.Equal(0xAB, x));

        _aplicador.Aplicar(P(new BarraTarefasPerfil(OcultarAutomaticamente: false)));
        Assert.Equal(0x02, Bytes()![8]);
    }

    [Fact]
    public void Stuck_rects_curto_vira_nao_aplicado()
    {
        _registro.Gravar(new ValorRegistro(Stuck, "Settings", TipoValor.Binario, new byte[] { 1, 2, 3, 4 }));
        var r = _aplicador.Aplicar(P(new BarraTarefasPerfil(OcultarAutomaticamente: true, Selos: true)));
        Assert.False(r.Ok);
        Assert.Contains(r.NaoAplicados, n => n.Valor.Nome == "Settings");
        Assert.Equal(1u, Dword(Advanced, "TaskbarBadges"));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Bytes());
    }

    [Fact]
    public void Avisos()
    {
        _aplicador.Aplicar(P(new BarraTarefasPerfil(Pesquisa: PesquisaBarra.Oculta)));
        Assert.Contains("barra", _notificador.Chamadas);
        Assert.DoesNotContain(_notificador.Chamadas, c => c.StartsWith("ocultar", StringComparison.Ordinal));

        _notificador.Chamadas.Clear();
        _aplicador.Aplicar(P(new BarraTarefasPerfil(OcultarAutomaticamente: true)));
        Assert.Contains("ocultar:True", _notificador.Chamadas);

        _notificador.Chamadas.Clear();
        _aplicador.Reverter(_retratos.Ultimo()!);
        Assert.Contains("ocultar:False", _notificador.Chamadas);
        Assert.Equal(0x02, Bytes()![8]);
    }

    [Fact]
    public void Retrato_captura_os_valores_da_barra()
    {
        _registro.Gravar(new ValorRegistro(Search, "SearchboxTaskbarMode", TipoValor.Dword, 1u));
        var r = _retratos.Tirar(null);
        Assert.Contains(new ValorRegistro(Search, "SearchboxTaskbarMode", TipoValor.Dword, 1u), r.Valores);
        Assert.Contains(r.Valores, v => v.Nome == "Settings" && v.Chave == Stuck);
        Assert.Contains(r.Valores, v => v.Nome == "ShowSystrayDateTimeValueName");
    }

    private sealed class NotificadorGravador : INotificador
    {
        public List<string> Chamadas { get; } = [];
        public void AvisarMudancaDeTema() => Chamadas.Add("tema");
        public void AvisarMudancaDaBarra() => Chamadas.Add("barra");
        public void AvisarOcultarAutomatico(bool ligado) => Chamadas.Add($"ocultar:{ligado}");
    }
}

using Vitral.Core.Nativo;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Tests;

public sealed class ServicoRetratosTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "vitral-testes-" + Guid.NewGuid().ToString("N"));
    private readonly RegistroEmMemoria _registro = new();

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public void Tirar_registra_valor_inexistente_como_ausente()
    {
        var servico = new ServicoRetratos(_registro, _pasta);
        var r = servico.Tirar(null);
        Assert.Equal(AjustesNativos.Controlados.Count, r.Valores.Count);
        Assert.All(r.Valores, v => Assert.True(v.Ausente));
    }

    [Fact]
    public void Tirar_registra_o_valor_atual()
    {
        var c = AjustesNativos.Controlados[0];
        _registro.Gravar(new ValorRegistro(c.Chave, c.Nome, c.Tipo, c.Tipo == TipoValor.Binario ? new byte[] { 1, 2 } : (object)1u));
        var r = new ServicoRetratos(_registro, _pasta).Tirar("teste");
        Assert.False(r.Valores[0].Ausente);
        Assert.Equal("teste", r.PerfilAplicado);
    }

    [Fact]
    public void Salvar_e_Ultimo_preservam_binario_e_ausente()
    {
        var servico = new ServicoRetratos(_registro, _pasta);
        var paleta = new byte[32]; paleta[3] = 0xFF;
        _registro.Gravar(new ValorRegistro(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", "AccentPalette", TipoValor.Binario, paleta));
        var r = servico.Tirar(null);
        servico.Salvar(r);
        var lido = servico.Ultimo();
        Assert.NotNull(lido);
        Assert.Equal(r.Valores, lido!.Valores);
    }

    [Fact]
    public void Ultimo_e_o_salvo_por_ultimo_mesmo_com_o_mesmo_horario()
    {
        var servico = new ServicoRetratos(_registro, _pasta);
        var quando = DateTimeOffset.Now;
        servico.Salvar(new Retrato(quando, "primeiro", []));
        servico.Salvar(new Retrato(quando, "segundo", []));
        Assert.Equal("segundo", servico.Ultimo()!.PerfilAplicado);
    }

    [Fact]
    public void GarantirOriginal_so_cria_na_primeira_vez()
    {
        var servico = new ServicoRetratos(_registro, _pasta);
        var primeiro = servico.GarantirOriginal();
        var segundo = servico.GarantirOriginal();
        Assert.Equal(primeiro.Quando, segundo.Quando);
        Assert.True(File.Exists(Path.Combine(_pasta, "original.json")));
    }

    [Fact]
    public void Historico_guarda_10_e_nunca_apaga_o_original()
    {
        var servico = new ServicoRetratos(_registro, _pasta);
        servico.GarantirOriginal();
        for (int i = 0; i < 12; i++) servico.Salvar(servico.Tirar($"p{i}"));
        Assert.Equal(10, servico.Historico().Count);
        Assert.Equal("p11", servico.Ultimo()!.PerfilAplicado);
        Assert.True(File.Exists(Path.Combine(_pasta, "original.json")));
    }
}

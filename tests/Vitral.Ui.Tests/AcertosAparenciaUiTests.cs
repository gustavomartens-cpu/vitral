using Vitral.Core.Perfis;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class AcertosAparenciaUiTests : IDisposable
{
    private readonly Ambiente _amb = new();
    public void Dispose() => _amb.Dispose();

    [Fact]
    public void Cor_fora_da_paleta_aparece_como_personalizada_e_selecionavel()
    {
        var e = new EditorAjustesAparencia();
        Assert.Equal(Opcoes.FundosAparencia, e.CoresFundo);
        e.CorFundo = "#123456";
        var extra = Assert.Single(e.CoresFundo, o => Equals(o.Valor, "#123456"));
        Assert.Contains("Personalizada", extra.Rotulo);
        Assert.Equal(Opcoes.FundosAparencia.Count + 1, e.CoresFundo.Count);
        e.CorDestaque = "#ABCDEF";
        Assert.Single(e.CoresDestaque, o => Equals(o.Valor, "#ABCDEF"));
    }

    [Fact]
    public void Cor_da_paleta_em_minusculas_marca_o_quadrado_dela()
    {
        var e = new EditorAjustesAparencia { CorDestaque = "#f2b544" };
        Assert.Equal(Opcoes.DestaquesAparencia.Count, e.CoresDestaque.Count);
        var ambar = Assert.Single(e.CoresDestaque, o => o.Rotulo == "Âmbar");
        Assert.Equal("#f2b544", ambar.Valor);
    }

    [Fact]
    public void Previa_das_formas_segue_os_cantos_da_aparencia_no_editor()
    {
        var vm = new PrincipalViewModel(_amb.Servico, _amb.Perfis, new DialogosFalsos(), new RelogioFalso())
        {
            CantosEmVigor = () => CantosParte.Suaves,
        };
        vm.EditorFormas.Recalcular();
        double suaves = vm.EditorFormas.Previa.Notch.Raio;
        vm.EditorAparencia.Tema = TemaAparencia.Grafite;
        vm.EditorAparencia.Geral.Cantos = CantosParte.Retos;
        double retos = vm.EditorFormas.Previa.Notch.Raio;
        vm.EditorAparencia.Geral.Cantos = CantosParte.Redondos;
        double redondos = vm.EditorFormas.Previa.Notch.Raio;
        Assert.True(retos < suaves && suaves < redondos, $"retos {retos}, suaves {suaves}, redondos {redondos}");
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, false)]
    public void Barra_unica_so_tem_fundo_com_alguma_ilha_do_lado(bool unica, bool midia, bool status, bool esperado) =>
        Assert.Equal(esperado, PosicaoIlhas.ComFundo(unica, midia, status));
}

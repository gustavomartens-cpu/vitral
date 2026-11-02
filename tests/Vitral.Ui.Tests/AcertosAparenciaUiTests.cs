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
    public void Trocar_de_cor_personalizada_para_outra_nao_perde_a_cor()
    {
        // Imita a ListBox: quando a lista troca e o item marcado não está nela, a seleção vira nula e volta para o campo.
        var e = new EditorAjustesAparencia();
        Opcao<object>? marcado = null;
        e.PropertyChanged += (_, a) =>
        {
            if (a.PropertyName == nameof(e.CoresFundo) && marcado is not null && !e.CoresFundo.Contains(marcado)) { marcado = null; e.CorFundo = null; }
            if (a.PropertyName == nameof(e.CorFundo)) marcado = e.CoresFundo.FirstOrDefault(o => Equals(o.Valor, e.CorFundo ?? Opcoes.Nulo));
        };
        e.Carregar(new Core.Perfis.AjustesAparencia(CorFundo: "#123456"));
        e.Carregar(new Core.Perfis.AjustesAparencia(CorFundo: "#654321"));
        Assert.Equal("#654321", e.CorFundo);
        e.Carregar(new Core.Perfis.AjustesAparencia(CorFundo: "#0A0F20"));
        Assert.Equal("#0A0F20", e.CorFundo);
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

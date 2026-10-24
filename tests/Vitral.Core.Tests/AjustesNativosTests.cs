using Vitral.Core.Cores;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Tests;

public class AjustesNativosTests
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string Dwm = @"Software\Microsoft\Windows\DWM";
    private const string Accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string Desktop = @"Control Panel\Desktop";
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    private static AjustesNativosPerfil Vazio() => new(null, null, null, null, null, null, null, null);

    private static ValorRegistro Achar(IReadOnlyList<ValorRegistro> vs, string chave, string nome) =>
        Assert.Single(vs, v => v.Chave == chave && v.Nome == nome);

    [Fact]
    public void Tudo_nulo_nao_gera_nenhum_valor()
    {
        Assert.Empty(AjustesNativos.Calcular(Vazio()));
    }

    [Fact]
    public void Modo_claro_forca_destaque_na_barra_desligado()
    {
        var vs = AjustesNativos.Calcular(Vazio() with { ModoSistema = Modo.Claro, DestaqueNaBarraEIniciar = true });
        Assert.Equal(1u, Achar(vs, Personalize, "SystemUsesLightTheme").Dado);
        Assert.Equal(0u, Achar(vs, Personalize, "ColorPrevalence").Dado);
    }

    [Fact]
    public void Destaque_na_barra_no_modo_escuro_fica_ligado()
    {
        var vs = AjustesNativos.Calcular(Vazio() with { ModoSistema = Modo.Escuro, DestaqueNaBarraEIniciar = true });
        Assert.Equal(1u, Achar(vs, Personalize, "ColorPrevalence").Dado);
    }

    [Fact]
    public void Cor_de_destaque_grava_DWM_em_ABGR_e_desliga_cor_automatica()
    {
        var vs = AjustesNativos.Calcular(Vazio() with { CorDestaque = "#21375E" });
        Assert.Equal(0xFF5E3721u, Achar(vs, Dwm, "AccentColor").Dado);
        Assert.Equal(0xC421375Eu, Achar(vs, Dwm, "ColorizationColor").Dado);
        Assert.Equal(0u, Achar(vs, Desktop, "AutoColorization").Dado);
        Assert.Equal(TipoValor.Binario, Achar(vs, Accent, "AccentPalette").Tipo);
    }

    [Fact]
    public void Cor_automatica_explicita_vence_quando_nao_ha_cor_fixa()
    {
        var vs = AjustesNativos.Calcular(Vazio() with { CorDestaqueAutomatica = true });
        Assert.Equal(1u, Achar(vs, Desktop, "AutoColorization").Dado);
    }

    [Fact]
    public void Menus_usam_os_tons_4_e_5_da_paleta()
    {
        var c = Cor.DeHex("#21375E");
        var paleta = AjustesNativos.Paleta(c);
        var vs = AjustesNativos.Calcular(Vazio() with { CorDestaque = "#21375E" });
        Assert.Equal(new Cor(paleta[12], paleta[13], paleta[14]).ParaAbgr(), Achar(vs, Accent, "AccentColorMenu").Dado);
        Assert.Equal(new Cor(paleta[16], paleta[17], paleta[18]).ParaAbgr(), Achar(vs, Accent, "StartColorMenu").Dado);
    }

    [Fact]
    public void Paleta_tem_32_bytes_tom_4_igual_a_base_e_ultimo_fixo()
    {
        var p = AjustesNativos.Paleta(Cor.DeHex("#21375E"));
        Assert.Equal(32, p.Length);
        Assert.Equal(new byte[] { 0x21, 0x37, 0x5E, 0xFF }, p[12..16]);
        Assert.Equal(new byte[] { 0x88, 0x17, 0x98, 0x00 }, p[28..32]);
    }

    [Fact]
    public void Paleta_vai_do_mais_claro_ao_mais_escuro()
    {
        var p = AjustesNativos.Paleta(Cor.DeHex("#21375E"));
        int Brilho(int i) => p[i * 4] + p[i * 4 + 1] + p[i * 4 + 2];
        for (int i = 0; i < 6; i++) Assert.True(Brilho(i) > Brilho(i + 1), $"tom {i} deveria ser mais claro que o tom {i + 1}");
    }

    [Fact]
    public void Alinhamento_esquerda_grava_TaskbarAl_zero()
    {
        var vs = AjustesNativos.Calcular(Vazio() with { AlinhamentoBarra = Alinhamento.Esquerda });
        Assert.Equal(0u, Achar(vs, Advanced, "TaskbarAl").Dado);
    }

    [Fact]
    public void Todo_valor_calculado_esta_na_lista_de_controlados()
    {
        var tudo = new AjustesNativosPerfil(Modo.Escuro, Modo.Claro, "#E81123", false, true, true, true, Alinhamento.Centro);
        foreach (var v in AjustesNativos.Calcular(tudo))
            Assert.Contains(AjustesNativos.Controlados, c =>
                string.Equals(c.Chave, v.Chave, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Nome, v.Nome, StringComparison.OrdinalIgnoreCase) && c.Tipo == v.Tipo);
    }
}

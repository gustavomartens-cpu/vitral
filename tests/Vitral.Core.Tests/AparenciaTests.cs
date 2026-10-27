using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.Core.Tests;

public sealed class AparenciaTests
{
    private static readonly VisualWindows Escuro = new(ModoEscuro: true, CorDestaque: "#0078D4", Transparencia: true);
    private static readonly VisualWindows ClaroWin = Escuro with { ModoEscuro = false };

    private static ValoresAparencia R(AparenciaPerfil? a, VisualWindows? w = null) => ResolverAparencia.Resolver(a, w ?? Escuro);
    private static CorArgb C(string hex, byte alfa = 255) => CorArgb.DeHex(hex, alfa)!.Value;

    [Theory]
    [InlineData(TemaAparencia.VidroEscuro, MaterialParte.Vidro, "#0A0F20", 184, "#F2B544", MaterialParte.Solido, "#0B0C10", 255)]
    [InlineData(TemaAparencia.PretoSolido, MaterialParte.Solido, "#0B0C10", 255, "#F2F2F2", MaterialParte.Solido, "#0B0C10", 255)]
    [InlineData(TemaAparencia.Claro, MaterialParte.Vidro, "#FAFAFC", 184, "#2F6FED", MaterialParte.Solido, "#F4F4F6", 255)]
    [InlineData(TemaAparencia.VidroColorido, MaterialParte.Vidro, "#783CB4", 107, "#FFD1FF", MaterialParte.Vidro, "#461E6E", 217)]
    [InlineData(TemaAparencia.Grafite, MaterialParte.Solido, "#24262B", 255, "#3DDBB5", MaterialParte.Solido, "#1A1B1F", 255)]
    [InlineData(TemaAparencia.Cristal, MaterialParte.Vidro, "#FFFFFF", 20, "#FFFFFF", MaterialParte.Vidro, "#000000", 140)]
    public void Tema_da_os_valores_da_tabela(TemaAparencia tema, MaterialParte material, string fundo, byte alfa, string destaque,
        MaterialParte materialNotch, string fundoNotch, byte alfaNotch)
    {
        var v = R(new AparenciaPerfil(tema));
        foreach (var parte in new[] { v.Doca, v.Ilhas })
        {
            Assert.Equal(material, parte.Material);
            Assert.Equal(C(fundo, alfa), parte.Fundo);
            Assert.Equal(C(destaque), parte.Destaque);
            Assert.Equal(CantosParte.Redondos, parte.Cantos);
        }
        Assert.Equal(materialNotch, v.Notch.Material);
        Assert.Equal(C(fundoNotch, alfaNotch), v.Notch.Fundo);
    }

    [Fact]
    public void Automatico_segue_o_modo_e_o_destaque_do_Windows()
    {
        var escuro = R(new AparenciaPerfil(TemaAparencia.Automatico), Escuro);
        Assert.Equal(C("#141826", 140), escuro.Ilhas.Fundo);
        Assert.Equal(C("#0078D4"), escuro.Ilhas.Destaque);
        Assert.Equal(C("#0B0C10"), escuro.Notch.Fundo);

        var claro = R(new AparenciaPerfil(TemaAparencia.Automatico), ClaroWin with { CorDestaque = "#E81123" });
        Assert.Equal(C("#FAFAFC", 184), claro.Doca.Fundo);
        Assert.Equal(C("#E81123"), claro.Doca.Destaque);
        Assert.Equal(C("#F4F4F6"), claro.Notch.Fundo);
    }

    [Fact]
    public void Camadas_tema_geral_parte()
    {
        var v = R(new AparenciaPerfil(TemaAparencia.Grafite,
            Geral: new AjustesAparencia(CorFundo: "#1F6F5C"),
            Notch: new AjustesAparencia(CorFundo: "#0B0C10", Cantos: CantosParte.Retos)));
        Assert.Equal(C("#1F6F5C"), v.Doca.Fundo);
        Assert.Equal(C("#1F6F5C"), v.Ilhas.Fundo);
        Assert.Equal(C("#0B0C10"), v.Notch.Fundo);
        Assert.Equal(CantosParte.Retos, v.Notch.Cantos);
        Assert.Equal(CantosParte.Redondos, v.Ilhas.Cantos);
    }

    [Fact]
    public void Texto_legivel()
    {
        Assert.Equal(C("#1B1D22"), R(new AparenciaPerfil(TemaAparencia.Claro)).Ilhas.Texto);
        Assert.Equal(C("#ECEEF2"), R(new AparenciaPerfil(TemaAparencia.Cristal)).Ilhas.Texto); // branco a 8%: quem aparece é o papel de parede
        Assert.Equal(C("#1B1D22"), R(new AparenciaPerfil(TemaAparencia.PretoSolido, Geral: new AjustesAparencia(CorFundo: "#F4F4F6"))).Doca.Texto);
        var claro = R(new AparenciaPerfil(TemaAparencia.Claro)).Ilhas;
        Assert.Equal(C("#1B1D22", 158), claro.TextoSecundario); // 62%
        Assert.Equal(new CorArgb(20, 0, 0, 0), claro.Borda); // preto a 8%
        Assert.Equal(new CorArgb(26, 255, 255, 255), R(new AparenciaPerfil(TemaAparencia.VidroEscuro)).Ilhas.Borda); // branco a 10%
    }

    [Fact]
    public void Transparencia_do_Windows_desligada_vira_solido()
    {
        var v = R(new AparenciaPerfil(TemaAparencia.VidroColorido), Escuro with { Transparencia = false });
        foreach (var parte in new[] { v.Doca, v.Ilhas, v.Notch })
        {
            Assert.Equal(MaterialParte.Solido, parte.Material);
            Assert.Equal(255, parte.Fundo.A);
            Assert.False(parte.Desfoque);
        }
    }

    [Fact]
    public void Notch_nunca_com_desfoque()
    {
        var v = R(new AparenciaPerfil(TemaAparencia.VidroColorido));
        Assert.Equal(MaterialParte.Vidro, v.Notch.Material);
        Assert.False(v.Notch.Desfoque);
        Assert.True(v.Ilhas.Desfoque);
        Assert.True(v.Doca.Desfoque);
    }

    [Fact]
    public void Transparencia_e_recortada()
    {
        Assert.Equal(255, R(new AparenciaPerfil(TemaAparencia.VidroEscuro, Geral: new AjustesAparencia(Transparencia: -10))).Ilhas.Fundo.A);
        Assert.Equal(0, R(new AparenciaPerfil(TemaAparencia.VidroEscuro, Geral: new AjustesAparencia(Transparencia: 140))).Ilhas.Fundo.A);
        Assert.Equal(128, R(new AparenciaPerfil(TemaAparencia.VidroEscuro, Geral: new AjustesAparencia(Transparencia: 50))).Ilhas.Fundo.A);
    }

    [Fact]
    public void Cor_invalida_e_ignorada() =>
        Assert.Equal(C("#24262B"), R(new AparenciaPerfil(TemaAparencia.Grafite, Geral: new AjustesAparencia(CorFundo: "azul", CorDestaque: "#12"))).Ilhas.Fundo);

    [Fact]
    public void Perfil_sem_aparencia_nao_mexe()
    {
        Assert.Equal(R(new AparenciaPerfil(TemaAparencia.VidroEscuro)), R(null));
        var sem = new Perfil(1, "x", null, null);
        Assert.Null(PerfilJson.Ler(PerfilJson.Escrever(sem)).Aparencia);
        var com = sem with { Aparencia = new AparenciaPerfil(TemaAparencia.Cristal, Notch: new AjustesAparencia(Material: MaterialParte.Solido)) };
        Assert.Equal(com.Aparencia, PerfilJson.Ler(PerfilJson.Escrever(com)).Aparencia);
    }

    [Theory]
    [InlineData(CantosParte.Retos, 0)]
    [InlineData(CantosParte.Suaves, 4)]
    [InlineData(CantosParte.Redondos, 8)]
    public void Cantos_em_px(CantosParte cantos, double px) =>
        Assert.Equal(px, R(new AparenciaPerfil(TemaAparencia.VidroEscuro, Geral: new AjustesAparencia(Cantos: cantos))).Doca.RaioPx);

    private static double Lum(CorArgb c)
    {
        static double K(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * K(c.R) + 0.7152 * K(c.G) + 0.0722 * K(c.B);
    }

    private static double Contraste(CorArgb a, CorArgb b)
    {
        double x = Lum(a), y = Lum(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    // Revisão final, Important 1: no Cristal o fundo é branco quase transparente e o texto é claro; a pílula principal
    // (fundo = texto) e o menu (fundo quase opaco) não podem derivar a cor do fundo da parte.
    [Theory]
    [InlineData(TemaAparencia.Cristal)]
    [InlineData(TemaAparencia.Claro)]
    [InlineData(TemaAparencia.VidroEscuro)]
    [InlineData(TemaAparencia.VidroColorido)]
    public void Pilula_e_menu_ficam_legiveis_em_todo_tema(TemaAparencia tema)
    {
        foreach (var p in new[] { R(new(tema)).Doca, R(new(tema)).Notch, R(new(tema)).Ilhas })
        {
            Assert.True(Contraste(p.Texto, p.TextoInverso) >= 4.5, $"{tema}: pílula");
            Assert.True(Contraste(p.Texto, p.FundoMenu.ComAlfa(255)) >= 4.5, $"{tema}: menu");
        }
        var cristalClaro = R(new(TemaAparencia.Cristal, new AjustesAparencia(CorFundo: "#F4F4F6", Transparencia: 70))).Ilhas;
        Assert.True(Contraste(cristalClaro.Texto, cristalClaro.FundoMenu.ComAlfa(255)) >= 4.5);
    }
}

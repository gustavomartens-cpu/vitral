using System.Globalization;

namespace Vitral.Ui.Barra;

/// Medidas do notch vivo (spec do notch vivo §3).
public static class NotchMedidas
{
    public const double AlturaNormal = 34, AlturaAviso = 52, LarguraMaxAviso = 360, RaioNormal = 16, RaioAviso = 22, ExtraLembrete = 14;
    public const double LarguraLateral = 130, LarguraMaxCorpo = 460;
    public const double AlturaPermissao = 112, LarguraMaxPermissao = 420, RaioPermissao = 24;
    /// Ficha de resposta (spec da resposta §4.2): largura fixa; a altura acompanha a mensagem, até o máximo.
    public const double LarguraResposta = 460, AlturaMaxResposta = 300;
    public const double JanelaLargura = LarguraMaxCorpo + 2 * FormaNotch.Orelha;
    public const double JanelaAltura = PosicaoIlhas.NotchSobra + AlturaMaxResposta;
}

/// O desenho do notch: o corpo com os cantos de baixo arredondados e as duas orelhas (quarto de círculo côncavo) que o
/// emendam na borda de cima. Origem no topo da orelha esquerda; largura total = largura + 2 × orelha.
public static class FormaNotch
{
    public const double Orelha = 16;

    /// <param name="orelha">0 = sem orelhas (lados retos até a borda de cima).</param>
    /// <param name="cantosDeCima">Formato Ilha (spec das formas §4.2): solto da borda, os quatro cantos arredondados.</param>
    public static string Caminho(double largura, double altura, double raio, double orelha = Orelha, bool cantosDeCima = false)
    {
        static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        if (cantosDeCima)
        {
            double r = Math.Min(raio, Math.Min(largura, altura) / 2);
            return string.Join(" ",
                $"M0,{N(r)}",
                $"A{N(r)},{N(r)} 0 0 1 {N(r)},0",
                $"L{N(largura - r)},0",
                $"A{N(r)},{N(r)} 0 0 1 {N(largura)},{N(r)}",
                $"L{N(largura)},{N(altura - r)}",
                $"A{N(r)},{N(r)} 0 0 1 {N(largura - r)},{N(altura)}",
                $"L{N(r)},{N(altura)}",
                $"A{N(r)},{N(r)} 0 0 1 0,{N(altura - r)}",
                "Z");
        }
        double total = largura + 2 * orelha, direita = orelha + largura;
        raio = Math.Min(raio, Math.Min(largura / 2, Math.Max(0, altura - orelha)));
        var partes = new List<string> { "M0,0", $"L{N(total)},0" };
        // orelha direita: da borda de cima até o lado do corpo, curva para dentro
        if (orelha > 0) partes.Add($"A{N(orelha)},{N(orelha)} 0 0 0 {N(direita)},{N(orelha)}");
        partes.Add($"L{N(direita)},{N(altura - raio)}");
        partes.Add($"A{N(raio)},{N(raio)} 0 0 1 {N(direita - raio)},{N(altura)}");
        partes.Add($"L{N(orelha + raio)},{N(altura)}");
        partes.Add($"A{N(raio)},{N(raio)} 0 0 1 {N(orelha)},{N(altura - raio)}");
        if (orelha > 0)
        {
            partes.Add($"L{N(orelha)},{N(orelha)}");
            partes.Add($"A{N(orelha)},{N(orelha)} 0 0 0 0,0");
        }
        partes.Add("Z");
        return string.Join(" ", partes);
    }
}

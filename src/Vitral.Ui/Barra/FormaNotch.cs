using System.Globalization;

namespace Vitral.Ui.Barra;

/// Medidas do notch vivo (spec do notch vivo §3).
public static class NotchMedidas
{
    public const double AlturaNormal = 34, AlturaAviso = 52, LarguraMaxAviso = 360, RaioNormal = 16, RaioAviso = 22, ExtraLembrete = 14;
    public const double JanelaLargura = LarguraMaxAviso + 2 * FormaNotch.Orelha;
    public const double JanelaAltura = PosicaoIlhas.NotchSobra + AlturaAviso;
}

/// O desenho do notch: o corpo com os cantos de baixo arredondados e as duas orelhas (quarto de círculo côncavo) que o
/// emendam na borda de cima. Origem no topo da orelha esquerda; largura total = largura + 2 × orelha.
public static class FormaNotch
{
    public const double Orelha = 16;

    public static string Caminho(double largura, double altura, double raio, double orelha = Orelha)
    {
        static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        double total = largura + 2 * orelha, direita = orelha + largura;
        raio = Math.Min(raio, Math.Min(largura / 2, Math.Max(0, altura - orelha)));
        return string.Join(" ",
            "M0,0",
            $"L{N(total)},0",
            // orelha direita: da borda de cima até o lado do corpo, curva para dentro
            $"A{N(orelha)},{N(orelha)} 0 0 0 {N(direita)},{N(orelha)}",
            $"L{N(direita)},{N(altura - raio)}",
            $"A{N(raio)},{N(raio)} 0 0 1 {N(direita - raio)},{N(altura)}",
            $"L{N(orelha + raio)},{N(altura)}",
            $"A{N(raio)},{N(raio)} 0 0 1 {N(orelha)},{N(altura - raio)}",
            $"L{N(orelha)},{N(orelha)}",
            $"A{N(orelha)},{N(orelha)} 0 0 0 0,0",
            "Z");
    }
}

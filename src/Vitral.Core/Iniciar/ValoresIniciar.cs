using System.Globalization;
using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.Core.Iniciar;

/// O que o componente pinta no Iniciar, a partir da aparência da doca (spec do Iniciar §3): vidro com a tinta da doca
/// (sólido, quando a doca é sólida ou a transparência do Windows está desligada), a borda e os cantos.
public sealed record ValoresIniciar(bool Vidro, CorArgb Tinta, CorArgb Borda, double Raio)
{
    public static ValoresIniciar De(ValoresParte doca) => new(
        doca.Desfoque,
        doca.Desfoque ? doca.Fundo : doca.Fundo.ComAlfa(255),
        doca.Borda,
        doca.Cantos switch { CantosParte.Retos => 0, CantosParte.Suaves => 4, _ => 8 });

    /// O arquivo de cores que o componente lê (plano, para um parse mínimo em C++). Desligado só leva o "ligado":
    /// o componente devolve o visual original.
    public string ParaJson(bool ligado) => ligado
        ? $$"""{"ligado":true,"vidro":{{(Vidro ? "true" : "false")}},"tinta":"{{Hex(Tinta)}}","borda":"{{Hex(Borda)}}","raio":{{Raio.ToString(CultureInfo.InvariantCulture)}}}"""
        : """{"ligado":false}""";

    private static string Hex(CorArgb c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}

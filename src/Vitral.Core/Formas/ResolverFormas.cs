using Vitral.Core.Perfis;

namespace Vitral.Core.Formas;

/// A doca em números. <paramref name="Icone"/> é a área do ícone (que a lupa amplia), <paramref name="Imagem"/> o desenho
/// dentro dela; <paramref name="Folga"/> é a distância até a borda de baixo.
public sealed record ValoresDoca(FormatoDoca Formato, double Icone, double Imagem, double BotaoLargura, double BotaoAltura,
    double Altura, double Folga, double Reserva, bool Ampliacao, bool CantosRetos);

/// O notch em repouso. <paramref name="Sobra"/>: px pintados acima da borda da tela (a costura com a borda);
/// <paramref name="Afastamento"/>: distância da borda até o topo visível (no formato Ilha).
public sealed record ValoresNotch(FormatoNotch Formato, double Altura, double EscalaTexto, double Orelha, double Sobra,
    double Afastamento, double Raio, bool CantosDeCima);

public sealed record ValoresIlhas(DisposicaoIlhas Disposicao, double Topo, double Borda, double Altura, bool CantosRetos);

public sealed record ValoresFormas(ValoresDoca Doca, ValoresNotch Notch, ValoresIlhas Ilhas, double ReservaTopo);

/// A regra das formas (spec das formas §4): cada opção vira números; nulo é a forma de hoje.
public static class ResolverFormas
{
    public static ValoresFormas Resolver(FormasPerfil? formas, CantosParte cantosNotch)
    {
        var doca = Doca(formas?.Doca);
        var notch = Notch(formas?.Notch, cantosNotch);
        var ilhas = Ilhas(formas?.Ilhas);
        double respiro = formas?.Ilhas?.Distancia == DistanciaIlhas.Encostadas ? 0 : 8;
        double reserva = Math.Max(ilhas.Topo + ilhas.Altura, notch.Afastamento + notch.Altura) + respiro;
        return new ValoresFormas(doca, notch, ilhas, reserva);
    }

    private static ValoresDoca Doca(FormasDoca? f)
    {
        var formato = f?.Formato ?? FormatoDoca.Flutuante;
        var (icone, altura) = (f?.Icones ?? TamanhoIcones.Medio) switch
        {
            TamanhoIcones.Pequeno => (24.0, 38.0),
            TamanhoIcones.Grande => (38.0, 56.0),
            _ => (30.0, 46.0),
        };
        bool corrida = formato == FormatoDoca.Corrida;
        double folga = corrida ? 0 : 8;
        return new ValoresDoca(formato, icone, icone * 0.8, icone + 10, icone + 14, altura, folga,
            corrida ? altura : altura + 2 * folga, f?.Ampliacao ?? true, corrida);
    }

    private static ValoresNotch Notch(FormasNotch? f, CantosParte cantos)
    {
        var formato = f?.Formato ?? FormatoNotch.Encaixado;
        var (altura, escala) = (f?.Tamanho ?? TamanhoNotch.Normal) switch
        {
            TamanhoNotch.Compacto => (28.0, 0.85),
            TamanhoNotch.Grande => (40.0, 1.15),
            _ => (34.0, 1.0),
        };
        bool ilha = formato == FormatoNotch.Ilha;
        double raio = cantos switch
        {
            CantosParte.Retos => 4,
            CantosParte.Suaves => 10,
            _ => ilha ? altura / 2 : 16,
        };
        return new ValoresNotch(formato, altura, escala, formato == FormatoNotch.Encaixado ? 16 : 0, ilha ? 0 : 10,
            ilha ? 6 : 0, raio, ilha);
    }

    private static ValoresIlhas Ilhas(FormasIlhas? f)
    {
        bool encostadas = f?.Distancia == DistanciaIlhas.Encostadas;
        return new ValoresIlhas(f?.Disposicao ?? DisposicaoIlhas.Separadas, encostadas ? 0 : 8, encostadas ? 0 : 12, 30, encostadas);
    }
}

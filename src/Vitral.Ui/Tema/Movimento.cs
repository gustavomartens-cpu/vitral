using Vitral.Ui.Barra;

namespace Vitral.Ui.Tema;

/// Lupa da doca (spec do ajuste visual §3.3): o ícone sob o cursor cresce até 1,22 e os vizinhos conforme a distância.
public static class Lupa
{
    public const double Maxima = 1.22, Alcance = 90;

    /// Distância horizontal, em DIPs, do cursor ao centro do botão.
    public static double Escala(double distancia) => 1 + (Maxima - 1) * Math.Max(0, 1 - Math.Abs(distancia) / Alcance);
}

/// Indicador único que desliza até o item escolhido (abas e pílulas).
public static class Deslize
{
    /// Posição e largura do indicador, relativas ao painel dos itens.
    public static (double X, double Largura) Alvo(Retangulo item, Retangulo painel) => (item.X - painel.X, item.Largura);
}

/// Conteúdo dos painéis do topo entrando um item atrás do outro.
public static class Cascata
{
    private static readonly TimeSpan Passo = TimeSpan.FromMilliseconds(40), Teto = TimeSpan.FromMilliseconds(200);

    public static TimeSpan Atraso(int indice) => indice <= 0 ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Min(Passo.Ticks * indice, Teto.Ticks));
}

/// Chave geral: com "Efeitos de animação" desligado no Windows, tudo vai direto ao estado final.
public static class TempoMovimento
{
    public static TimeSpan Duracao(TimeSpan normal, bool ligado) => ligado ? normal : TimeSpan.Zero;
}

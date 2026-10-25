namespace Vitral.Ui.Barra;

public readonly record struct Retangulo(double X, double Y, double Largura, double Altura);

/// Onde ficam as três ilhas, em DIPs, a partir dos limites inteiros do monitor principal
/// (não da área de trabalho, que já começa abaixo da reserva). Spec §3.1.
public static class PosicaoIlhas
{
    public const double Topo = 8, Borda = 12, Altura = 30, Reserva = 46;

    /// Retângulo de um monitor em pixels para DIPs, pela escala do próprio monitor (não a da inicialização do app).
    public static Retangulo ParaDips(int x, int y, int largura, int altura, uint dpi)
    {
        double escala = dpi == 0 ? 1 : dpi / 96.0;
        return new Retangulo(x / escala, y / escala, largura / escala, altura / escala);
    }

    public static (Retangulo Midia, Retangulo Relogio, Retangulo Status) Calcular(
        Retangulo monitor, double larguraMidia, double larguraRelogio, double larguraStatus)
    {
        double y = monitor.Y + Topo;
        return (
            new Retangulo(monitor.X + Borda, y, larguraMidia, Altura),
            new Retangulo(monitor.X + (monitor.Largura - larguraRelogio) / 2, y, larguraRelogio, Altura),
            new Retangulo(monitor.X + monitor.Largura - Borda - larguraStatus, y, larguraStatus, Altura));
    }
}

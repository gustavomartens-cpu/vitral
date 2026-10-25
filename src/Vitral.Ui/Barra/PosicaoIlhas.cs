namespace Vitral.Ui.Barra;

public readonly record struct Retangulo(double X, double Y, double Largura, double Altura);

public enum AlinhamentoPainel { Esquerda, Centro, Direita }

/// Onde ficam as três ilhas, em DIPs, a partir dos limites inteiros do monitor principal
/// (não da área de trabalho, que já começa abaixo da reserva). Spec §3.1.
public static class PosicaoIlhas
{
    public const double Topo = 8, Borda = 12, Altura = 30, Reserva = 46;

    /// Notch do relógio (spec v2 §3.1): a janela começa 10 px acima da borda, para os cantos de cima (que o DWM
    /// arredonda junto com os de baixo) ficarem fora da tela; 34 px visíveis.
    public const double NotchSobra = 10, NotchAltura = 44;

    /// Retângulo de um monitor em pixels para DIPs, pela escala do próprio monitor (não a da inicialização do app).
    public static Retangulo ParaDips(int x, int y, int largura, int altura, uint dpi)
    {
        double escala = dpi == 0 ? 1 : dpi / 96.0;
        return new Retangulo(x / escala, y / escala, largura / escala, altura / escala);
    }

    /// Doca (spec da doca §3): 46 px de altura, 8 px acima da borda de baixo, centralizada; reserva de 62 px.
    public const double DocaAltura = 46, DocaFolga = 8, ReservaDoca = 62;

    public static Retangulo Doca(Retangulo monitor, double largura) =>
        new(monitor.X + (monitor.Largura - largura) / 2, monitor.Y + monitor.Altura - DocaFolga - DocaAltura, largura, DocaAltura);

    /// Distância entre a ilha e o painel que desce dela (spec v2 §3.2).
    public const double FolgaPainel = 6;

    /// Onde um painel desce: 6 px abaixo da ilha, alinhado a ela e nunca a menos de 12 px das bordas do monitor.
    public static Retangulo Painel(Retangulo ilha, double largura, double altura, AlinhamentoPainel alinhamento, Retangulo monitor)
    {
        double x = alinhamento switch
        {
            AlinhamentoPainel.Esquerda => ilha.X,
            AlinhamentoPainel.Centro => ilha.X + (ilha.Largura - largura) / 2,
            _ => ilha.X + ilha.Largura - largura,
        };
        x = Math.Clamp(x, monitor.X + Borda, Math.Max(monitor.X + Borda, monitor.X + monitor.Largura - Borda - largura));
        return new Retangulo(x, ilha.Y + ilha.Altura + FolgaPainel, largura, altura);
    }

    public static (Retangulo Midia, Retangulo Relogio, Retangulo Status) Calcular(
        Retangulo monitor, double larguraMidia, double larguraRelogio, double larguraStatus)
    {
        double y = monitor.Y + Topo;
        return (
            new Retangulo(monitor.X + Borda, y, larguraMidia, Altura),
            new Retangulo(monitor.X + (monitor.Largura - larguraRelogio) / 2, monitor.Y - NotchSobra, larguraRelogio, NotchAltura),
            new Retangulo(monitor.X + monitor.Largura - Borda - larguraStatus, y, larguraStatus, Altura));
    }
}

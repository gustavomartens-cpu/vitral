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

    public static Retangulo Doca(Retangulo monitor, double largura) => Doca(monitor, largura, Padrao.Doca);

    /// Doca pelas formas (spec das formas §4.1): a corrida ocupa a largura do monitor, encostada embaixo.
    public static Retangulo Doca(Retangulo monitor, double largura, Core.Formas.ValoresDoca doca) =>
        doca.Formato == Core.Perfis.FormatoDoca.Corrida
            ? new(monitor.X, monitor.Y + monitor.Altura - doca.Altura, monitor.Largura, doca.Altura)
            : new(monitor.X + (monitor.Largura - largura) / 2, monitor.Y + monitor.Altura - doca.Folga - doca.Altura, largura, doca.Altura);

    /// As formas de hoje (tudo nulo).
    public static Core.Formas.ValoresFormas Padrao { get; } = Core.Formas.ResolverFormas.Resolver(null, Core.Perfis.CantosParte.Redondos);

    /// Miniaturas da doca: 10 px acima dela, centralizadas no botão e a pelo menos 12 px das bordas do monitor.
    public static Retangulo Miniaturas(Retangulo doca, double centroBotao, double largura, double altura, Retangulo monitor)
    {
        double x = Math.Clamp(centroBotao - largura / 2, monitor.X + 12, Math.Max(monitor.X + 12, monitor.X + monitor.Largura - 12 - largura));
        return new Retangulo(x, doca.Y - 10 - altura, largura, altura);
    }

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
        Retangulo monitor, double larguraMidia, double larguraRelogio, double larguraStatus) =>
        Calcular(monitor, larguraMidia, larguraRelogio, larguraStatus, Padrao.Ilhas, Padrao.Notch);

    /// Pelas formas (spec das formas §4.2 e §4.3): a janela do notch começa <c>Sobra</c> px acima da borda (ou
    /// <c>Afastamento</c> abaixo dela, no formato Ilha).
    public static (Retangulo Midia, Retangulo Relogio, Retangulo Status) Calcular(Retangulo monitor, double larguraMidia,
        double larguraRelogio, double larguraStatus, Core.Formas.ValoresIlhas ilhas, Core.Formas.ValoresNotch notch)
    {
        double y = monitor.Y + ilhas.Topo;
        return (
            new Retangulo(monitor.X + ilhas.Borda, y, larguraMidia, ilhas.Altura),
            new Retangulo(monitor.X + (monitor.Largura - larguraRelogio) / 2, monitor.Y - notch.Sobra + notch.Afastamento,
                larguraRelogio, notch.Sobra + notch.Altura),
            new Retangulo(monitor.X + monitor.Largura - ilhas.Borda - larguraStatus, y, larguraStatus, ilhas.Altura));
    }

    /// Barra única (spec das formas §4.3): de lado a lado, dentro das bordas, no topo das ilhas.
    public static Retangulo Barra(Retangulo monitor, Core.Formas.ValoresIlhas ilhas) =>
        new(monitor.X + ilhas.Borda, monitor.Y + ilhas.Topo, monitor.Largura - 2 * ilhas.Borda, ilhas.Altura);
}

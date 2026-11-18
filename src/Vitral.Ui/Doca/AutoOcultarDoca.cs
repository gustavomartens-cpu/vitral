using Vitral.Ui.Barra;

namespace Vitral.Ui.Doca;

public enum AcaoDoca { Nenhuma, Mostrar, Esconder }

/// Regra da doca que se esconde sozinha (spec de 17/11/2026): some 500 ms depois da última vez em que o mouse esteve
/// nela ou na borda (ou em que algo a travou: menu, miniaturas, arraste) e aparece com o mouse 100 ms seguidos na
/// faixa de 2 px de baixo do monitor principal. Tela cheia ganha da borda. Sem relógio nem Win32: o App chama
/// <see cref="Conferir"/> a cada leitura do cursor e executa a ação devolvida.
public sealed class AutoOcultarDoca
{
    public static readonly TimeSpan EsperaAparecer = TimeSpan.FromMilliseconds(100), EsperaSumir = TimeSpan.FromMilliseconds(500);
    public const double FaixaPx = 2;

    private DateTime _ultimoUso;
    private DateTime? _inicioBorda;

    public bool Visivel { get; private set; } = true;

    /// O cursor (em pixels) nos 2 px de baixo do monitor, dentro da largura dele.
    public static bool NaBorda(double x, double y, Retangulo monitor) =>
        x >= monitor.X && x < monitor.X + monitor.Largura && y >= monitor.Y + monitor.Altura - FaixaPx && y < monitor.Y + monitor.Altura;

    /// O cursor sobre a doca, contando o vão entre ela e a borda de baixo (a flutuante fica 8 DIPs acima).
    public static bool SobreDoca(double x, double y, Retangulo doca, Retangulo monitor) =>
        x >= doca.X && x < doca.X + doca.Largura && y >= doca.Y && y < monitor.Y + monitor.Altura;

    /// Ligar a opção (visível, começa a contar para sumir) ou sair da tela cheia (escondida).
    public void Reiniciar(DateTime agora, bool visivel)
    {
        Visivel = visivel;
        _ultimoUso = agora;
        _inicioBorda = null;
    }

    public AcaoDoca Conferir(DateTime agora, bool naBorda, bool sobreDoca, bool travada, bool telaCheia)
    {
        if (Visivel)
        {
            if (naBorda || sobreDoca || travada) { _ultimoUso = agora; return AcaoDoca.Nenhuma; }
            if (agora - _ultimoUso < EsperaSumir) return AcaoDoca.Nenhuma;
            Reiniciar(agora, visivel: false);
            return AcaoDoca.Esconder;
        }
        if (!naBorda || telaCheia) { _inicioBorda = null; return AcaoDoca.Nenhuma; }
        _inicioBorda ??= agora;
        if (agora - _inicioBorda.Value < EsperaAparecer) return AcaoDoca.Nenhuma;
        Reiniciar(agora, visivel: true);
        return AcaoDoca.Mostrar;
    }
}

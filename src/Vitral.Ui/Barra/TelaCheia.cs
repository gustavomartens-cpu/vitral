namespace Vitral.Ui.Barra;

/// O aviso de "app em tela cheia" do Windows (ABN_FULLSCREENAPP) também chega quando a própria área de trabalho
/// (Progman/WorkerW, onde o Wallpaper Engine desenha) vai para o primeiro plano, e às vezes por tela cheia em outro
/// monitor. Só vale se a janela em primeiro plano cobre o monitor principal inteiro e não é a área de trabalho.
public static class TelaCheia
{
    private static readonly HashSet<string> AreaDeTrabalho = new(StringComparer.Ordinal) { "Progman", "WorkerW", "Shell_TrayWnd" };

    public static bool EhDeVerdade(string classeDaJanela, Retangulo janela, Retangulo monitorPrincipal) =>
        !AreaDeTrabalho.Contains(classeDaJanela)
        && janela.X <= monitorPrincipal.X && janela.Y <= monitorPrincipal.Y
        && janela.X + janela.Largura >= monitorPrincipal.X + monitorPrincipal.Largura
        && janela.Y + janela.Altura >= monitorPrincipal.Y + monitorPrincipal.Altura;
}

/// O estado de tela cheia conferido pelo próprio Vitral (a janela da frente cobre o monitor principal), e não pelo aviso
/// do Windows, que falha com alguns jogos e manda "saiu" quando um overlay abre (pedido do Gustavo, 31/10). Devolve o
/// estado novo só quando muda.
public sealed class VigiaTelaCheia
{
    public bool EmTelaCheia { get; private set; }

    public bool? Conferir(bool cobreAgora)
    {
        if (cobreAgora == EmTelaCheia) return null;
        EmTelaCheia = cobreAgora;
        return cobreAgora;
    }
}

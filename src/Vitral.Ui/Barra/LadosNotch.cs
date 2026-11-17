namespace Vitral.Ui.Barra;

/// O que aparece à esquerda do relógio no notch fechado: o Claude ou, sem ele, a capa da música.
public enum LadoEsquerdo { Nada, Esperando, Rodando, Terminou, Capa }

/// Os dois lados do relógio: à esquerda o Claude (com o número de sessões esperando ou rodando) ou a capa; à direita a
/// música, com as barras do som e, quando o Claude ocupa a esquerda, a capa ao lado delas.
public readonly record struct Lados(LadoEsquerdo Esquerdo, int Numero, bool Musica)
{
    public bool Algum => Esquerdo != LadoEsquerdo.Nada || Musica;

    /// A capa vai para a direita, junto das barras, quando o Claude está à esquerda.
    public bool CapaADireita => Musica && Esquerdo != LadoEsquerdo.Capa;
}

/// A ordem dos lados (spec do notch fechado §2.1, R8, fixa): à esquerda, esperando você > rodando > terminou > capa da
/// música; à direita, as barras sempre que há música (tocando ou pausada) e ela está ligada no perfil. Com o Claude à
/// esquerda, a capa acompanha as barras à direita; sem ele, fica à esquerda para os dois lados se equilibrarem
/// (Gustavo, 16/11/2026).
public static class LadosNotch
{
    /// Largura de cada lado quando algum tem conteúdo (os dois iguais, para o relógio ficar no centro): cabem a capa de
    /// 18 px, o respiro e as 5 barras.
    public const double LarguraLado = 52;

    public static Lados Decidir(ResumoAtividade a, EstadoMidia? midia, bool midiaLigada)
    {
        bool musica = midia is not null && midiaLigada;
        var (esquerdo, numero) = a switch
        {
            { Esperando: > 0 } => (LadoEsquerdo.Esperando, a.Esperando),
            { Rodando: > 0 } => (LadoEsquerdo.Rodando, a.Rodando),
            { Terminou: true } => (LadoEsquerdo.Terminou, 0),
            _ when musica => (LadoEsquerdo.Capa, 0),
            _ => (LadoEsquerdo.Nada, 0),
        };
        return new(esquerdo, numero, musica);
    }
}

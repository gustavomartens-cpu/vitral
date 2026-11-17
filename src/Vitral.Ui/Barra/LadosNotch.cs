namespace Vitral.Ui.Barra;

/// O que aparece à esquerda do relógio no notch fechado: só o Claude.
public enum LadoEsquerdo { Nada, Esperando, Rodando, Terminou }

/// Os dois lados do relógio: o Claude à esquerda, com o número ao lado (sessões esperando ou rodando), e a música à
/// direita (capa pequena e barras do som).
public readonly record struct Lados(LadoEsquerdo Esquerdo, int Numero, bool Musica)
{
    public bool Algum => Esquerdo != LadoEsquerdo.Nada || Musica;
}

/// A ordem dos lados (spec do notch fechado §2.1, R8, fixa): à esquerda, esperando você > rodando > terminou; à direita,
/// a capa e as barras sempre que há música (tocando ou pausada) e ela está ligada no perfil. A capa ficava à esquerda
/// sem o Claude; o Gustavo a quis à direita, junto das barras (16/11/2026).
public static class LadosNotch
{
    /// Largura de cada lado quando algum tem conteúdo (os dois iguais, para o relógio ficar no centro): cabem a capa de
    /// 18 px, o respiro e as 5 barras.
    public const double LarguraLado = 52;

    public static Lados Decidir(ResumoAtividade a, EstadoMidia? midia, bool midiaLigada)
    {
        var (esquerdo, numero) = a switch
        {
            { Esperando: > 0 } => (LadoEsquerdo.Esperando, a.Esperando),
            { Rodando: > 0 } => (LadoEsquerdo.Rodando, a.Rodando),
            { Terminou: true } => (LadoEsquerdo.Terminou, 0),
            _ => (LadoEsquerdo.Nada, 0),
        };
        return new(esquerdo, numero, midia is not null && midiaLigada);
    }
}

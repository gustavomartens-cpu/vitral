namespace Vitral.Ui.Barra;

/// O que aparece à esquerda do relógio no notch fechado.
public enum LadoEsquerdo { Nada, Esperando, Rodando, Terminou, Capa }

/// Os dois lados do relógio: o esquerdo, o número ao lado dele (sessões esperando ou rodando) e as barras do som à direita.
public readonly record struct Lados(LadoEsquerdo Esquerdo, int Numero, bool Barras)
{
    public bool Algum => Esquerdo != LadoEsquerdo.Nada || Barras;
}

/// A ordem dos lados (spec do notch fechado §2.1, R8, fixa): à esquerda, esperando você > rodando > terminou > capa da
/// música; à direita, as barras sempre que há música (tocando ou pausada) e ela está ligada no perfil.
public static class LadosNotch
{
    /// Largura de cada lado quando algum tem conteúdo (os dois iguais, para o relógio ficar no centro).
    public const double LarguraLado = 40;

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

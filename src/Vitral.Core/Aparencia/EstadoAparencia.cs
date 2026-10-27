using Vitral.Core.Configuracao;
using Vitral.Core.Perfis;

namespace Vitral.Core.Aparencia;

/// Quem desenha a doca, o notch e as ilhas com a aparência (o ServicoAparencia no App).
public interface IControleAparencia
{
    void Aplicar(AparenciaPerfil aparencia);
}

/// A aparência aplicada (config.json), no molde da barra do topo e da doca (spec da aparência §3 e §5.3).
public sealed class EstadoAparencia(IControleAparencia controle, ServicoConfig config)
{
    public static AparenciaPerfil Padrao { get; } = new(TemaAparencia.VidroEscuro);

    /// Sem nada guardado, o visual de sempre: Vidro escuro.
    public AparenciaPerfil Atual() => config.Ler().Aparencia ?? Padrao;

    /// Nulo é "não mexer".
    public void Aplicar(AparenciaPerfil? aparencia)
    {
        if (aparencia is null) return;
        config.Gravar(config.Ler() with { Aparencia = aparencia });
        controle.Aplicar(aparencia);
    }

    /// Ao abrir o App: a aparência guardada.
    public void AoIniciar() => controle.Aplicar(Atual());
}

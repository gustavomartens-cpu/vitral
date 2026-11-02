using Vitral.Core.Configuracao;

namespace Vitral.Core.Iniciar;

/// Quem pinta o Iniciar (o ServicoIniciar no App).
public interface IControleIniciar
{
    void Aplicar(ModoIniciar modo);
}

/// O modo do Iniciar aplicado (config.json), no molde da aparência e das formas (spec do Iniciar §2).
public sealed class EstadoIniciar(IControleIniciar controle, ServicoConfig config)
{
    /// Sem nada guardado, o Iniciar do Windows: até aqui ninguém o pintava.
    public static IniciarPerfil Padrao { get; } = new(ModoIniciar.Windows);

    public IniciarPerfil AtualPerfil() => config.Ler().Iniciar is { Modo: not null } guardado ? guardado : Padrao;

    public ModoIniciar Atual() => AtualPerfil().Modo!.Value;

    /// Nulo, ou modo nulo, é "não mexer".
    public void Aplicar(IniciarPerfil? iniciar)
    {
        if (iniciar?.Modo is not { } modo) return;
        config.Gravar(config.Ler() with { Iniciar = iniciar });
        controle.Aplicar(modo);
    }

    /// Ao abrir o App: o modo guardado.
    public void AoIniciar() => controle.Aplicar(Atual());
}

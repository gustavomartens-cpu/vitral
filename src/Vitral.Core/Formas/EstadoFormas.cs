using Vitral.Core.Configuracao;
using Vitral.Core.Perfis;

namespace Vitral.Core.Formas;

/// Quem muda a forma da doca, do notch e das ilhas (o ServicoFormas no App).
public interface IControleFormas
{
    void Aplicar(FormasPerfil formas);
}

/// As formas aplicadas (config.json), no molde da aparência (spec das formas §5.1).
public sealed class EstadoFormas(IControleFormas controle, ServicoConfig config)
{
    /// Tudo nulo: as formas de sempre.
    public static FormasPerfil Padrao { get; } = new();

    public FormasPerfil Atual() => config.Ler().Formas ?? Padrao;

    /// Nulo é "não mexer".
    public void Aplicar(FormasPerfil? formas)
    {
        if (formas is null) return;
        config.Gravar(config.Ler() with { Formas = formas });
        controle.Aplicar(formas);
    }

    /// Ao abrir o App: as formas guardadas.
    public void AoIniciar() => controle.Aplicar(Atual());
}

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

    /// Nulo é "não mexer", e cada campo nulo também: o perfil muda só o que escolheu.
    public void Aplicar(FormasPerfil? formas)
    {
        if (formas is null) return;
        Gravar(ResolverFormas.Mesclar(Atual(), formas));
    }

    /// Volta a um retrato (testar, desfazer, original): substitui tudo, para um campo que estava em "não mexer" voltar
    /// também. Retrato sem formas (de antes desta versão): não mexe.
    public void Restaurar(FormasPerfil? formas)
    {
        if (formas is null) return;
        Gravar(formas);
    }

    private void Gravar(FormasPerfil formas)
    {
        config.Gravar(config.Ler() with { Formas = formas });
        controle.Aplicar(formas);
    }

    /// Ao abrir o App: as formas guardadas.
    public void AoIniciar() => controle.Aplicar(Atual());
}

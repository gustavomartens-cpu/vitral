using Vitral.Core.Configuracao;
using Vitral.Core.Perfis;

namespace Vitral.Core.Barra;

/// Estado aplicado da barra do topo: o controle real mais o que fica guardado no config.json.
public sealed class EstadoBarra(IControleBarra controle, ServicoConfig config)
{
    /// O que está aplicado agora; sem registro no config, a barra está desligada (o Windows não tem barra própria).
    public BarraTopoPerfil Atual() => config.Ler().BarraTopo ?? BarraTopoPerfil.Desligada;

    /// Nulo é "não mexer".
    public void Aplicar(BarraTopoPerfil? estado)
    {
        if (estado is null) return;
        controle.Aplicar(estado);
        config.Gravar(config.Ler() with { BarraTopo = estado });
    }
}

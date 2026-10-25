using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Barra;

/// Estado aplicado da barra do topo: o controle real mais o que fica guardado no config.json.
/// Com <paramref name="registro"/>, também cuida do relógio de baixo: ele some só enquanto a barra do topo existe de
/// verdade (ligada pelo App) e volta quando ela desliga ou o Vitral fecha. Sem registro (linha de comando), não toca.
public sealed class EstadoBarra(IControleBarra controle, ServicoConfig config, IRegistro? registro = null, INotificador? notificador = null)
{
    /// O que está aplicado agora; sem registro no config, a barra está desligada (o Windows não tem barra própria).
    public BarraTopoPerfil Atual() => config.Ler().BarraTopo ?? BarraTopoPerfil.Desligada;

    /// Nulo é "não mexer".
    public void Aplicar(BarraTopoPerfil? estado)
    {
        if (estado is null) return;
        controle.Aplicar(estado);
        config.Gravar(config.Ler() with { BarraTopo = estado });
        AjustarRelogio(estado);
    }

    /// Ao abrir o App: mostra a barra como estava e esconde o relógio de novo se ela estiver ligada.
    public void Mostrar()
    {
        var estado = Atual();
        controle.Aplicar(estado);
        AjustarRelogio(estado);
    }

    /// Ao fechar o App: a barra do topo some com ele, então o relógio de baixo volta.
    public void Encerrar() => AjustarRelogio(BarraTopoPerfil.Desligada);

    private void AjustarRelogio(BarraTopoPerfil estado)
    {
        if (registro is null || BarraDeTarefas.Relogio(estado) is not { } valor) return;
        try
        {
            registro.Gravar(valor);
            notificador?.AvisarMudancaDaBarra();
        }
        catch (EscritaBloqueadaException) { /* o relógio fica como está; o resto da barra não depende dele */ }
    }
}

using Vitral.Core.Perfis;

namespace Vitral.Core.Barra;

/// Liga ou desliga a doca de verdade (janela e reserva). O App implementa; nunca lança.
public interface IControleDoca
{
    void Aplicar(BarraBaixoPerfil estado);
}

/// A barra de tarefas do Windows vista de fora: esconder, mostrar e o "ocultar automaticamente".
public interface IBarraDoWindows
{
    /// Null se não deu para ler.
    bool? OcultarAutomatico();
    void DefinirOcultarAutomatico(bool ligado);
    void Esconder();
    void Mostrar();
}

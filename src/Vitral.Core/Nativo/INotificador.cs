namespace Vitral.Core.Nativo;

/// Avisa o Windows de que as configurações mudaram, para ele redesenhar sem precisar sair e entrar.
public interface INotificador
{
    void AvisarMudancaDeTema();
    void AvisarMudancaDaBarra();

    /// Liga ou desliga o "ocultar automaticamente" da barra de tarefas na hora (ABM_SETSTATE).
    void AvisarOcultarAutomatico(bool ligado);
}

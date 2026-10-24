namespace Vitral.Core.Nativo;

/// Avisa o Windows de que as configurações mudaram, para ele redesenhar sem precisar sair e entrar.
public interface INotificador
{
    void AvisarMudancaDeTema();
    void AvisarMudancaDaBarra();
}

namespace Vitral.Core.Perfis;

/// Seção "barraBaixo" do perfil (esquema 4): a doca do Vitral no lugar da barra de tarefas do Windows.
/// Seção ausente ou nula = não mexer.
public sealed record BarraBaixoPerfil(bool Visivel)
{
    public static BarraBaixoPerfil Desligada { get; } = new(false);
}

namespace Vitral.Core.Perfis;

/// Seção "barraBaixo" do perfil (esquema 4): a doca do Vitral no lugar da barra de tarefas do Windows.
/// Seção ausente ou nula = não mexer. <paramref name="EsconderAutomatico"/>: a doca some e solta a reserva, voltando
/// quando o mouse encosta na borda de baixo (spec de 17/11/2026); nulo = mantém o aplicado.
public sealed record BarraBaixoPerfil(bool Visivel, bool? EsconderAutomatico = null)
{
    public static BarraBaixoPerfil Desligada { get; } = new(false);
}

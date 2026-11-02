namespace Vitral.Core.Iniciar;

/// O Menu Iniciar (spec do Iniciar §2): com o visual do Vitral (o vidro da doca) ou com o do Windows.
public enum ModoIniciar { Vitral, Windows }

/// Seção "iniciar" do perfil. A seção nula, ou o modo nulo, é "não mexer".
public sealed record IniciarPerfil(ModoIniciar? Modo = null);

namespace Vitral.Core.Configuracao;

/// Um "Testar" que ainda não foi confirmado nem cancelado. <see cref="Retrato"/> é o arquivo do retrato
/// salvo antes do teste: se o app morrer no meio, ao abrir de novo volta a ele.
public sealed record TesteEmAndamento(string Perfil, string Retrato);

/// Preferências do app (%LOCALAPPDATA%\Vitral\config.json). <see cref="BarraTopo"/> é o estado aplicado da barra do topo.
public sealed record ConfigApp(string? PerfilAtivo, bool IniciarComWindows, TesteEmAndamento? TesteEmAndamento,
    Perfis.BarraTopoPerfil? BarraTopo = null, IReadOnlyList<string>? IconesVisiveis = null,
    Perfis.BarraBaixoPerfil? BarraBaixo = null, bool? OcultarAutomaticoAntesDaDoca = null)
{
    public static ConfigApp Padrao { get; } = new(null, true, null);
}

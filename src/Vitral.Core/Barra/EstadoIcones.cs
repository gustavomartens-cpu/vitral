using Vitral.Core.Configuracao;

namespace Vitral.Core.Barra;

/// A lista de ícones à mostra que está aplicada (config.json), para o observador tratar os ícones que aparecem depois.
/// Nula = "não mexer" (ou o estado voltou de um retrato): o observador não faz nada.
public sealed class EstadoIcones(ServicoConfig config)
{
    public IReadOnlyList<string>? Atual() => config.Ler().IconesVisiveis;

    public void Definir(IReadOnlyList<string>? lista) => config.Gravar(config.Ler() with { IconesVisiveis = lista });
}

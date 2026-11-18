using CommunityToolkit.Mvvm.ComponentModel;
using Vitral.Core.Perfis;

namespace Vitral.Ui;

/// Editor da seção barraBaixo do perfil (a doca). "Não mexer" vira null; "Desligada" devolve a barra do Windows.
public sealed partial class EditorDocaViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubstituiBarraDoWindows))]
    [NotifyPropertyChangedFor(nameof(PodeEsconder))]
    private Escolha _doca;

    /// "Esconder automaticamente" (spec de 17/11/2026): só vale com a doca ligada no perfil.
    [ObservableProperty] private Escolha _esconder;

    public bool PodeEsconder => Doca == Escolha.Sim;

    /// Com a doca ligada no perfil, a barra do Windows fica escondida: as opções dela não têm onde aparecer e a aba as
    /// recolhe (repaginação da janela, decisão do Gustavo em 29/10).
    public bool SubstituiBarraDoWindows => Doca == Escolha.Sim;

    public void Carregar(BarraBaixoPerfil? b)
    {
        Doca = b switch { null => Escolha.NaoMexer, { Visivel: true } => Escolha.Sim, _ => Escolha.Nao };
        Esconder = b?.EsconderAutomatico switch { true => Escolha.Sim, false => Escolha.Nao, null => Escolha.NaoMexer };
    }

    public BarraBaixoPerfil? ParaPerfil() => Doca switch
    {
        Escolha.Sim => new BarraBaixoPerfil(true, Esconder switch { Escolha.Sim => true, Escolha.Nao => false, _ => null }),
        Escolha.Nao => BarraBaixoPerfil.Desligada,
        _ => null,
    };
}

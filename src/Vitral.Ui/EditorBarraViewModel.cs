using CommunityToolkit.Mvvm.ComponentModel;
using Vitral.Core.Perfis;

namespace Vitral.Ui;

/// Editor da seção barraTopo do perfil. "Não mexer" vira null; "Não" vira a barra desligada.
/// As ilhas só valem (e só ficam habilitadas) com a barra ligada.
public sealed partial class EditorBarraViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IlhasHabilitadas))]
    private Escolha _barra;

    [ObservableProperty] private bool _midia = true;
    [ObservableProperty] private bool _relogio = true;
    [ObservableProperty] private bool _status = true;
    [ObservableProperty] private bool _desempenho = true;

    public bool IlhasHabilitadas => Barra == Escolha.Sim;

    public void Carregar(BarraTopoPerfil? b)
    {
        Barra = b switch { null => Escolha.NaoMexer, { Visivel: true } => Escolha.Sim, _ => Escolha.Nao };
        var ilhas = b?.IlhasEfetivas ?? IlhasBarra.Todas;
        Midia = ilhas.Midia;
        Relogio = ilhas.Relogio;
        Status = ilhas.Status;
        Desempenho = ilhas.Desempenho;
    }

    public BarraTopoPerfil? ParaPerfil() => Barra switch
    {
        Escolha.Sim => new BarraTopoPerfil(true, new IlhasBarra(Midia, Relogio, Status, Desempenho)),
        Escolha.Nao => BarraTopoPerfil.Desligada,
        _ => null,
    };
}

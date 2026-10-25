using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui.Barra;

/// Painel "Ícones" da barra do topo: a bandeja dos apps sem o que o topo já mostra (volume, bateria, rede). Os cliques vão ao app.
public sealed partial class PainelIconesViewModel : ObservableObject, IDisposable
{
    private readonly IFonteBandeja _fonte;

    [ObservableProperty] private bool _disponivel;

    public ObservableCollection<IconeBandeja> Icones { get; } = [];

    public PainelIconesViewModel(IFonteBandeja fonte)
    {
        _fonte = fonte;
        _fonte.Mudou += Ler;
        Ler();
    }

    private void Ler()
    {
        Disponivel = _fonte.Disponivel;
        Icones.Clear();
        foreach (var i in _fonte.Icones.Where(i => !i.JaNoTopo)) Icones.Add(i);
    }

    [RelayCommand] private void ClicarEsquerdo(IconeBandeja? icone) { if (icone is not null) _fonte.Clicar(icone.Id, BotaoMouse.Esquerdo); }
    [RelayCommand] private void ClicarDireito(IconeBandeja? icone) { if (icone is not null) _fonte.Clicar(icone.Id, BotaoMouse.Direito); }

    public void Dispose() => _fonte.Mudou -= Ler;
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Vitral.Ui.Barra;

/// Ilha do relógio: "HH:mm" e a data curta em pt-BR sem pontos ("sex, 23 out"), atualizadas a cada segundo.
public sealed partial class IlhaRelogioViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IRelogioDoSistema _agora;
    private readonly IDisposable _tique;

    [ObservableProperty] private string _hora = "";
    [ObservableProperty] private string _data = "";

    public IlhaRelogioViewModel(IRelogioDoSistema agora, IRelogio relogio)
    {
        _agora = agora;
        Atualizar();
        _tique = relogio.ACadaSegundo(Atualizar);
    }

    private void Atualizar()
    {
        var a = _agora.Agora;
        Hora = a.ToString("HH:mm", PtBr);
        Data = a.ToString("ddd, d MMM", PtBr).Replace(".", "").ToLowerInvariant();
    }

    public void Dispose() => _tique.Dispose();
}

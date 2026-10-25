using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui;

/// "Manter este visual?" com contagem regressiva. Zerar = voltar. Manter e voltar acontecem uma vez só.
public sealed partial class ContagemViewModel : ObservableObject
{
    private readonly Action _manter;
    private readonly Action _voltar;
    private IDisposable? _relogio;
    private bool _encerrada;

    [ObservableProperty] private int _restantes;

    public int Total { get; }

    public ContagemViewModel(int segundos, IRelogio relogio, Action manter, Action voltar)
    {
        Total = segundos;
        _restantes = segundos;
        _manter = manter;
        _voltar = voltar;
        _relogio = relogio.ACadaSegundo(Tique);
    }

    private void Tique()
    {
        if (_encerrada) return;
        Restantes = Math.Max(0, Restantes - 1);
        if (Restantes == 0) Voltar();
    }

    [RelayCommand]
    private void Manter() => Encerrar(_manter);

    [RelayCommand]
    private void Voltar() => Encerrar(_voltar);

    private void Encerrar(Action acao)
    {
        if (_encerrada) return;
        _encerrada = true;
        _relogio?.Dispose();
        _relogio = null;
        acao();
    }
}

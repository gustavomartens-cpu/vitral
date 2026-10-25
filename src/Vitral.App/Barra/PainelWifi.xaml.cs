using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// O PasswordBox não aceita binding (a senha não fica em propriedade comum): o código repassa o texto ao
/// ViewModel, limpa a caixa quando ele zera a senha e conecta com Enter.
public partial class PainelWifi : UserControl
{
    private PasswordBox? _caixa;
    private PainelWifiViewModel? _vm;

    public PainelWifi()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= AoMudarVm;
            _vm = DataContext as PainelWifiViewModel;
            if (_vm is not null) _vm.PropertyChanged += AoMudarVm;
        };
    }

    private void AoMudarVm(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PainelWifiViewModel.Senha) && _vm?.Senha == "" && _caixa is { Password.Length: > 0 })
            _caixa.Clear();
    }

    private void Senha_Carregada(object sender, RoutedEventArgs e)
    {
        _caixa = (PasswordBox)sender;
        _caixa.Clear();
        _caixa.Focus();
    }

    private void Senha_Mudou(object sender, RoutedEventArgs e)
    {
        _caixa = (PasswordBox)sender;
        if (_vm is not null) _vm.Senha = _caixa.Password;
    }

    private void Senha_Tecla(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm is null) return;
        e.Handled = true;
        _vm.ConectarComSenhaCommand.Execute(null);
    }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// O PasswordBox não aceita binding (a senha não fica em propriedade comum): o código repassa o texto ao
/// ViewModel, limpa a caixa quando ele zera a senha, dá o foco quando uma rede pede senha e conecta com Enter.
public partial class PainelWifi : UserControl
{
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
        if (e.PropertyName == nameof(PainelWifiViewModel.Senha) && _vm?.Senha == "" && CaixaSenha.Password.Length > 0)
            CaixaSenha.Clear();
        if (e.PropertyName == nameof(PainelWifiViewModel.PedindoSenha) && _vm?.PedindoSenha is not null)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => CaixaSenha.Focus()); // depois de ficar visível
    }

    private void Senha_Mudou(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.Senha = CaixaSenha.Password;
    }

    private void Senha_Tecla(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm is null) return;
        e.Handled = true;
        _vm.ConectarComSenhaCommand.Execute(null);
    }
}

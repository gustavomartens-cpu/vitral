using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// O deslizante de brilho não tem binding de volta (a tela é um registro imutável): o código repassa o valor ao
/// ViewModel só quando o usuário mexe, e o tique de 100 ms escreve o último valor que ficou esperando.
public partial class PainelControles : UserControl
{
    private readonly DispatcherTimer _tique = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public PainelControles()
    {
        InitializeComponent();
        _tique.Tick += (_, _) => (DataContext as PainelControlesViewModel)?.Tique();
        IsVisibleChanged += (_, _) => { if (IsVisible) _tique.Start(); else _tique.Stop(); };
    }

    private void Brilho_Mudou(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // O valor inicial vem do binding: só o mouse ou o teclado do usuário mexem no brilho.
        if (sender is not Slider { DataContext: TelaBrilho tela } s || !(s.IsMouseOver || s.IsKeyboardFocusWithin)) return;
        (DataContext as PainelControlesViewModel)?.MudarBrilho(tela.Id, (int)Math.Round(e.NewValue));
    }
}

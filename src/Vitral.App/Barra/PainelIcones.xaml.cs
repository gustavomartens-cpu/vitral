using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class PainelIcones : UserControl
{
    public PainelIcones() => InitializeComponent();

    /// Clique esquerdo ou direito repassado ao app dono do ícone.
    private void Icone_Clique(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PainelIconesViewModel vm || (sender as FrameworkElement)?.DataContext is not IconeBandeja icone) return;
        if (e.ChangedButton == MouseButton.Right)
        {
            (Window.GetWindow(this) as JanelaPainel)?.SegurarDuranteMenu();
            vm.ClicarDireitoCommand.Execute(icone);
        }
        else if (e.ChangedButton == MouseButton.Left) vm.ClicarEsquerdoCommand.Execute(icone);
        e.Handled = true;
    }
}

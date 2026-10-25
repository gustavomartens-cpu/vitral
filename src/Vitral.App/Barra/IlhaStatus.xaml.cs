using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class IlhaStatus : UserControl
{
    public IlhaStatus() => InitializeComponent();

    private IlhaStatusViewModel? Vm => DataContext as IlhaStatusViewModel;

    private void Volume_Roda(object sender, MouseWheelEventArgs e)
    {
        int passos = e.Delta / Mouse.MouseWheelDeltaForOneLine;
        Vm?.Rolar(passos == 0 ? Math.Sign(e.Delta) : passos);
        e.Handled = true;
    }

    private void Volume_Clique(object sender, MouseButtonEventArgs e) => Vm?.AlternarMudoCommand.Execute(null);

    private void Rapidas_Clique(object sender, MouseButtonEventArgs e) => Atalhos.ConfiguracoesRapidas();

    private void Desempenho_Clique(object sender, MouseButtonEventArgs e) => Atalhos.GerenciadorDeTarefas();
}

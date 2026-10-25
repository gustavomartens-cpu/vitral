using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class IlhaStatus : UserControl, IPedePainel
{
    public IlhaStatus() => InitializeComponent();

    public event Action<Painel>? Pediu;

    private IlhaStatusViewModel? Vm => DataContext as IlhaStatusViewModel;

    private void Volume_Roda(object sender, MouseWheelEventArgs e)
    {
        int passos = e.Delta / Mouse.MouseWheelDeltaForOneLine;
        Vm?.Rolar(passos == 0 ? Math.Sign(e.Delta) : passos);
        e.Handled = true;
    }

    private void Volume_Clique(object sender, MouseButtonEventArgs e) => Pediu?.Invoke(Painel.Volume);

    private void Rede_Clique(object sender, MouseButtonEventArgs e) => Pediu?.Invoke(Painel.Wifi);

    private void Bateria_Clique(object sender, MouseButtonEventArgs e) => Pediu?.Invoke(Painel.Bateria);

    private void Desempenho_Clique(object sender, MouseButtonEventArgs e) => Atalhos.GerenciadorDeTarefas();

    private void Icones_Clique(object sender, MouseButtonEventArgs e) => Pediu?.Invoke(Painel.Icones);

    /// A setinha ^ só aparece com a bandeja do Vitral disponível (doca ligada).
    public void MostrarSetinha(bool mostrar) => Setinha.Visibility = mostrar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
}

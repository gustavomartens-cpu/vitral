using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class IlhaMidia : UserControl, IPedePainel
{
    public IlhaMidia() => InitializeComponent();

    public event Action<Painel>? Pediu;

    private void Titulo_Clique(object sender, MouseButtonEventArgs e) => Pediu?.Invoke(Painel.Midia);
}

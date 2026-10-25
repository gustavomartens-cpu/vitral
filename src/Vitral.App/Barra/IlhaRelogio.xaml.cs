using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class IlhaRelogio : UserControl, IPedePainel
{
    public IlhaRelogio() => InitializeComponent();

    public event Action<Painel>? Pediu;

    private void Relogio_Clique(object sender, MouseButtonEventArgs e) => Pediu?.Invoke(Painel.Calendario);
}

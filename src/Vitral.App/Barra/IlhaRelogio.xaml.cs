using System.Windows.Controls;
using System.Windows.Input;

namespace Vitral.App.Barra;

public partial class IlhaRelogio : UserControl
{
    public IlhaRelogio() => InitializeComponent();

    private void Relogio_Clique(object sender, MouseButtonEventArgs e) => Atalhos.CentralDeNotificacoes();
}

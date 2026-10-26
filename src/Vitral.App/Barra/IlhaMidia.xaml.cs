using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class IlhaMidia : UserControl, IPedePainel
{
    public IlhaMidia() => InitializeComponent();

    public event Action<Painel>? Pediu;

    /// O mouse entrou (true) ou saiu (false) da capa e do título.
    public event Action<bool>? MouseNoTitulo;

    private void Titulo_Clique(object sender, MouseButtonEventArgs e) => Pediu?.Invoke(Painel.Midia);

    private void Gatilho_Entrou(object sender, MouseEventArgs e) => MouseNoTitulo?.Invoke(true);

    private void Gatilho_Saiu(object sender, MouseEventArgs e) => MouseNoTitulo?.Invoke(false);
}

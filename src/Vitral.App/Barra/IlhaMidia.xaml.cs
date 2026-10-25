using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class IlhaMidia : UserControl
{
    public IlhaMidia() => InitializeComponent();

    private void Titulo_Clique(object sender, MouseButtonEventArgs e) => (DataContext as IlhaMidiaViewModel)?.TrazerAppCommand.Execute(null);
}

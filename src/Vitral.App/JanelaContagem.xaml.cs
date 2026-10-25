using System.Windows;
using Vitral.Ui;

namespace Vitral.App;

/// Janelinha sempre visível no canto inferior direito durante o "Testar".
public partial class JanelaContagem : Window
{
    public JanelaContagem(ContagemViewModel vm)
    {
        InitializeComponent();
        Vidro.Aplicar(this);
        DataContext = vm;
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;
        };
    }
}

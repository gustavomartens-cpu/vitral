using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Vitral.Ui.Tema;

namespace Vitral.App.Tema;

/// ActualHeight → CornerRadius da pílula (metade da altura). Ligado com RelativeSource Self.
public sealed class MeiaAltura : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new CornerRadius(Pilula.Raio(value is double d ? d : 0));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

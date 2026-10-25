using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Vitral.Core.Cores;

namespace Vitral.App;

/// "#RRGGBB" → pincel; vazio ou inválido → transparente (a prévia some em vez de quebrar).
public sealed class CorParaPincel : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string texto) return Brushes.Transparent;
        try
        {
            var c = Cor.DeHex(texto.Trim());
            return new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// Texto não vazio → verdadeiro (habilita o editor só com um perfil escolhido).
public sealed class TemValor : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && s.Length > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

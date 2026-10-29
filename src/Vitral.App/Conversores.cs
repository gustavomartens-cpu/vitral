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

/// Aba Aparência: nulo ("do tema", "Não mexer") ↔ a marca <see cref="Ui.Opcoes.Nulo"/> das listas, que pode aparecer
/// selecionada.
public sealed class NuloComoMarca : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value ?? Ui.Opcoes.Nulo;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ReferenceEquals(value, Ui.Opcoes.Nulo) ? null : value;
}

/// Miniatura da grade de temas: o tema → os valores dele com o Windows de agora (a marca "Não mexer" → nulo).
public sealed class TemaParaValores : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Core.Perfis.TemaAparencia t
            ? Core.Aparencia.ResolverAparencia.Resolver(new Core.Perfis.AparenciaPerfil(t), Tema.ServicoAparencia.LerWindows()) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// Transparência do personalizar (0 a 100, nula = do tema) no deslizante: nula deixa o deslizante onde está.
public sealed class TransparenciaNoDeslizante : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int t ? (double)t : Binding.DoNothing;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? (int)Math.Round(d) : null;
}

/// Dois textos iguais, sem diferenciar maiúsculas (a cor do quadradinho é a cor escolhida no perfil).
public sealed class Iguais : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is string a && values[1] is string b && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

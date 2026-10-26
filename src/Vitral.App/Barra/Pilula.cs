using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vitral.App.Tema;

namespace Vitral.App.Barra;

/// Botão em pílula das fichas do notch, nas cores escolhidas pelo Gustavo em 26/10 (branco e vermelho): o principal
/// branco sólido, o de contorno claro, o de perigo com texto vermelho; afunda no clique.
internal static class Pilula
{
    public enum Estilo { Principal, Contorno, Perigo }

    private static SolidColorBrush Cor(byte a, byte r, byte g, byte b) { var p = new SolidColorBrush(Color.FromArgb(a, r, g, b)); p.Freeze(); return p; }

    public static Border Criar(string texto, Estilo estilo, Action aoClicar)
    {
        var (fundo, hover, borda, corTexto) = estilo switch
        {
            Estilo.Principal => (Cor(0xFF, 0xF2, 0xF2, 0xF2), Cor(0xFF, 0xFF, 0xFF, 0xFF), Brushes.Transparent, Cor(0xFF, 0x11, 0x11, 0x11)),
            Estilo.Perigo => ((Brush)Brushes.Transparent, Cor(0x29, 0xE8, 0x54, 0x54), Brushes.Transparent, Cor(0xFF, 0xFF, 0x8A, 0x8A)),
            _ => (Brushes.Transparent, Cor(0x14, 0xFF, 0xFF, 0xFF), Cor(0x47, 0xFF, 0xFF, 0xFF), Cor(0xFF, 0xE6, 0xE6, 0xE6)),
        };
        var rotulo = new TextBlock
        {
            Text = texto, FontSize = 11.5, Foreground = corTexto, FontWeight = estilo == Estilo.Principal ? FontWeights.SemiBold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var botao = new Border
        {
            Height = 28, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(3, 0, 3, 0),
            Cursor = Cursors.Hand, Child = rotulo, Background = fundo, BorderBrush = borda, BorderThickness = new Thickness(1),
        };
        botao.MouseEnter += (_, _) => botao.Background = hover;
        botao.MouseLeave += (_, _) => botao.Background = fundo;
        Efeitos.SetAfundar(botao, true);
        botao.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true; // o clique é do botão, não do notch (que abriria o calendário)
            if (botao.IsEnabled) aoClicar();
        };
        return botao;
    }
}

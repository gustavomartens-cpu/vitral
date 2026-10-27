using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Vitral.App.Tema;

namespace Vitral.App.Barra;

/// Botão em pílula das fichas do notch e da lista de pendências. As cores vêm da aparência da parte onde o botão está
/// (chaves PilulaFundo, PilulaTexto, PilulaBorda, Perigo... — o padrão é o branco e vermelho escolhido pelo Gustavo em
/// 26/10): o principal com fundo cheio, o de contorno com borda clara, o de perigo com texto vermelho; afunda no clique.
internal static class Pilula
{
    public enum Estilo { Principal, Contorno, Perigo }

    public static Border Criar(string texto, Estilo estilo, Action aoClicar)
    {
        var (fundo, hover, borda, corTexto) = estilo switch
        {
            Estilo.Principal => ("PilulaFundo", "PilulaFundoHover", (string?)null, "PilulaTexto"),
            Estilo.Perigo => ((string?)null, "PerigoHover", (string?)null, "Perigo"),
            _ => ((string?)null, "SuperficieHover", "PilulaBorda", "Texto"),
        };
        var rotulo = new TextBlock
        {
            Text = texto, FontSize = 11.5, FontWeight = estilo == Estilo.Principal ? FontWeights.SemiBold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        rotulo.SetResourceReference(TextBlock.ForegroundProperty, corTexto);
        var botao = new Border
        {
            Height = 28, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(3, 0, 3, 0),
            Cursor = Cursors.Hand, Child = rotulo, BorderThickness = new Thickness(1), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
        };
        void Repousar() { if (fundo is null) botao.Background = Brushes.Transparent; else botao.SetResourceReference(Border.BackgroundProperty, fundo); }
        Repousar();
        if (borda is not null) botao.SetResourceReference(Border.BorderBrushProperty, borda);
        botao.MouseEnter += (_, _) => botao.SetResourceReference(Border.BackgroundProperty, hover);
        botao.MouseLeave += (_, _) => Repousar();
        Efeitos.SetAfundar(botao, true);
        // O botão afunda (encolhe) ao ser apertado: perto da borda, o soltar cairia fora do desenho encolhido e o clique
        // se perderia. Por isso o botão prende o mouse ao apertar e, ao soltar, confere a posição contra o tamanho
        // normal dele.
        botao.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            botao.CaptureMouse();
        };
        botao.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true; // o clique é do botão, não do notch (que abriria o calendário)
            if (!botao.IsMouseCaptured) return;
            botao.ReleaseMouseCapture();
            if (VisualTreeHelper.GetParent(botao) is not UIElement pai) return;
            var ponto = e.GetPosition(pai);
            if (botao.IsEnabled && LayoutInformation.GetLayoutSlot(botao).Contains(ponto)) aoClicar();
        };
        return botao;
    }
}

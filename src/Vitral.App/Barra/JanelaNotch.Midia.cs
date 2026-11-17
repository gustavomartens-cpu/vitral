using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// O notch desce com a música (spec do notch fechado §2.2): com o mouse em cima e algo tocando, embaixo das laterais
/// aparece a linha da música (capa, título, artista e app, barras, botões e a linha do tempo). Substitui a ilha da mídia
/// e o painel que descia dela.
internal sealed partial class JanelaNotch
{
    private readonly Grid _linhaMidia = new() { Margin = new Thickness(18, 0, 18, 12), Opacity = 0, Visibility = Visibility.Collapsed, Background = Brushes.Transparent };
    private readonly Border _capaCartao = new() { Width = 46, Height = 46, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _tituloMidia = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Cursor = Cursors.Hand, ToolTip = "Abrir o app" };
    private readonly TextBlock _subtituloMidia = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Grid _tempoMidia = new() { Height = 3, Margin = new Thickness(0, 7, 0, 0) };
    private readonly ScaleTransform _fracaoMidia = new(0, 1);
    private readonly TextBlock _botaoTocar = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Rectangle[] _barrasCartao = new Rectangle[5];
    private PainelMidiaViewModel? _cartao;

    /// Título, artista, capa, posição e comandos da música (vive no ControleBarra, com as fontes).
    public PainelMidiaViewModel? CartaoMidia
    {
        get => _cartao;
        set
        {
            if (_cartao is not null) _cartao.PropertyChanged -= AoMudarCartao;
            _cartao = value;
            if (_cartao is not null) _cartao.PropertyChanged += AoMudarCartao;
            AtualizarCartao();
        }
    }

    private void MontarMidia()
    {
        _subtituloMidia.SetResourceReference(TextBlock.ForegroundProperty, "TextoSecundario");
        _tituloMidia.MouseLeftButtonUp += (_, e) => { _cartao?.AbrirAppCommand.Execute(null); e.Handled = true; };

        var trilho = new Border { CornerRadius = new CornerRadius(1.5) };
        trilho.SetResourceReference(Border.BackgroundProperty, "Linha");
        var feito = new Border { CornerRadius = new CornerRadius(1.5), RenderTransform = _fracaoMidia, RenderTransformOrigin = new Point(0, 0.5) };
        feito.SetResourceReference(Border.BackgroundProperty, "Texto");
        _tempoMidia.Children.Add(trilho);
        _tempoMidia.Children.Add(feito);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(_tituloMidia);
        textos.Children.Add(_subtituloMidia);
        textos.Children.Add(_tempoMidia);

        var barras = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Height = BarraMaxima, Margin = new Thickness(14, 0, 14, 0) };
        for (int i = 0; i < _barrasCartao.Length; i++)
        {
            _barrasCartao[i] = new Rectangle { Width = 3, Height = BarraMinima, RadiusX = 1.5, RadiusY = 1.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0) };
            barras.Children.Add(_barrasCartao[i]);
        }

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        botoes.Children.Add(BotaoMidia("", "Anterior", () => _cartao?.AnteriorCommand.Execute(null)));
        var tocar = BotaoMidia("", "Tocar ou pausar", () => _cartao?.TocarPausarCommand.Execute(null));
        _botaoTocar.Text = "";
        tocar.Child = _botaoTocar;
        _botaoTocar.FontFamily = (FontFamily)Application.Current.Resources["FonteIcones"];
        _botaoTocar.FontSize = 16;
        _botaoTocar.SetResourceReference(TextBlock.ForegroundProperty, "Ambar");
        botoes.Children.Add(tocar);
        botoes.Children.Add(BotaoMidia("", "Próxima", () => _cartao?.ProximaCommand.Execute(null)));

        var linha = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_capaCartao, Dock.Left);
        DockPanel.SetDock(botoes, Dock.Right);
        DockPanel.SetDock(barras, Dock.Right);
        linha.Children.Add(_capaCartao);
        linha.Children.Add(botoes);
        linha.Children.Add(barras);
        linha.Children.Add(textos);
        _linhaMidia.Children.Add(linha);

        _conteudo.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _conteudo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_linhaMidia, 1);
        Grid.SetColumnSpan(_linhaMidia, 3);
        _conteudo.Children.Add(_linhaMidia);
        Closed += (_, _) => CartaoMidia = null;
    }

    private static Border BotaoMidia(string glifo, string dica, Action clique)
    {
        var b = new Border
        {
            Width = 30, Height = 30, Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = dica,
            Child = new TextBlock
            {
                Text = glifo, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)Application.Current.Resources["FonteIcones"],
            },
        };
        b.MouseLeftButtonUp += (_, e) => { clique(); e.Handled = true; };
        return b;
    }

    /// O cartão desce com as laterais quando há música ligada no perfil.
    private bool CartaoVisivel => _estado.Laterais && _lados.Musica && _cartao is not null;

    private void AoMudarCartao(object? s, PropertyChangedEventArgs e) => AtualizarCartao();

    private void AtualizarCartao()
    {
        if (_cartao is not { } c) return;
        _tituloMidia.Text = c.Titulo;
        _subtituloMidia.Text = c.Subtitulo;
        _botaoTocar.Text = c.Tocando ? "" : "";
        _tempoMidia.Visibility = c.MostrarProgresso ? Visibility.Visible : Visibility.Collapsed;
        _fracaoMidia.ScaleX = Math.Clamp(c.Fracao, 0, 1);
        _capaCartao.Background = _imagemCapa is null ? null : new ImageBrush(_imagemCapa) { Stretch = Stretch.UniformToFill };
        foreach (var b in _barrasCartao) b.Fill = _corBarras;
    }
}

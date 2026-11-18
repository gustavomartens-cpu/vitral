using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Vitral.App.Tema;
using Vitral.Ui.Barra;
using Vitral.Ui.Tema;

namespace Vitral.App.Barra;

/// O notch fechado com atividades (spec do notch fechado §2.1, R1, R3, R8): à esquerda do relógio o Claude trabalhando
/// (spinner e número), esperando (âmbar) ou que acabou de terminar (check), ou, sem ele, a capa da música; à direita as
/// barras do som que sai (ou dançando sozinhas, quando o som toca noutro aparelho), com a capa ao lado quando o Claude
/// ocupa a esquerda. Os dois lados têm a mesma largura, para o relógio ficar no centro.
internal sealed partial class JanelaNotch
{
    // O ✳ (U+2733) sai como emoji com moldura no Windows; o ✶ ocupa o lugar dele. Fonte de símbolos, nunca a de emoji.
    private static readonly string[] Glifos = ["·", "✢", "✶", "✻", "✽", "✻", "✶", "✢"];
    private static readonly Brush VerdeCheck = Congelado(new SolidColorBrush(Color.FromRgb(0x97, 0xC4, 0x59)));
    private const double BarraMinima = 3, BarraMaxima = 16, BarraParada = 8;

    private readonly Grid _ladoEsquerdo = new() { Width = LadosNotch.LarguraLado, Visibility = Visibility.Collapsed };
    private readonly Grid _ladoDireito = new() { Width = LadosNotch.LarguraLado, Visibility = Visibility.Collapsed };
    private readonly TextBlock _spinner = new() { FontSize = 14, FontFamily = new FontFamily("Segoe UI Symbol"), VerticalAlignment = VerticalAlignment.Center, Foreground = CorClaude, Width = 14, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _numero = new() { FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) };
    private readonly Ellipse _pontoEspera = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _check = new() { Text = "✓", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = VerdeCheck, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _capaEsquerda = new() { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _capaPequena = new() { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Rectangle[] _barras = new Rectangle[5];
    private readonly DispatcherTimer _tiqueSpinner = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private CapturaSom? _captura;
    private readonly SomOuDanca _somOuDanca = new();
    private int _glifo;
    private Lados _lados;

    private AtividadeClaude? _atividade;
    private IFonteMidia? _midia;
    private bool _midiaLigada = true;
    private byte[]? _capaLida;
    private ImageSource? _imagemCapa;
    private Brush _corBarras = Brushes.Gray;

    /// As sessões do Claude (vive no ControleBarra, para não perder a contagem quando o notch é refeito).
    public AtividadeClaude? Atividade
    {
        get => _atividade;
        set
        {
            if (_atividade is not null) _atividade.Mudou -= AoMudarAtividade;
            _atividade = value;
            if (_atividade is not null) _atividade.Mudou += AoMudarAtividade;
            AoMudarAtividade();
        }
    }

    /// A música do Windows (a mesma fonte da ilha de antes).
    public IFonteMidia? Midia
    {
        get => _midia;
        set
        {
            if (_midia is not null) _midia.Mudou -= AoMudarAtividade;
            _midia = value;
            if (_midia is not null) _midia.Mudou += AoMudarAtividade;
            AoMudarAtividade();
        }
    }

    /// A opção "Mídia" do perfil: a música no notch.
    public bool MidiaLigada
    {
        get => _midiaLigada;
        set { _midiaLigada = value; AoMudarAtividade(); }
    }

    private void MontarLados()
    {
        _pontoEspera.SetResourceReference(Shape.FillProperty, "Ambar");
        _numero.SetResourceReference(TextBlock.ForegroundProperty, "Texto");
        var esquerda = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        esquerda.Children.Add(_spinner);
        esquerda.Children.Add(_pontoEspera);
        esquerda.Children.Add(_check);
        esquerda.Children.Add(_capaEsquerda);
        esquerda.Children.Add(_numero);
        _ladoEsquerdo.Children.Add(esquerda);

        var direita = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Height = BarraMaxima };
        _capaPequena.Margin = new Thickness(0, 0, 6, 0);
        direita.Children.Add(_capaPequena);
        for (int i = 0; i < _barras.Length; i++)
        {
            _barras[i] = new Rectangle { Width = 3, Height = BarraMinima, RadiusX = 1.5, RadiusY = 1.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0) };
            direita.Children.Add(_barras[i]);
        }
        _ladoDireito.Children.Add(direita);

        _linhaRelogio.Children.Insert(0, _ladoEsquerdo);
        _linhaRelogio.Children.Add(_ladoDireito);
        _tiqueSpinner.Tick += (_, _) => { _glifo = (_glifo + 1) % Glifos.Length; _spinner.Text = Glifos[_glifo]; };
        IsVisibleChanged += (_, _) => AtualizarCaptura();
        Closed += (_, _) =>
        {
            _tiqueSpinner.Stop();
            _captura?.Dispose();
            Atividade = null;
            Midia = null;
        };
    }

    private void AoMudarAtividade()
    {
        if (!IsLoaded) return;
        var antes = _lados;
        AtualizarLados();
        if (antes.Algum != _lados.Algum || antes.Musica != _lados.Musica) Ajustar(animar: true);
    }

    /// Os lados mostrados agora (escondidos com as laterais abertas ou fora do modo normal).
    private bool LadosVisiveis => _lados.Algum && !_estado.Laterais && _estado.Modo is ModoNotch.Normal or ModoNotch.Lembrete;

    /// Decide e pinta os lados; liga o spinner e a captura só quando aparecem.
    private void AtualizarLados()
    {
        var midia = _midia?.Atual;
        _lados = LadosNotch.Decidir(_atividade?.Resumo ?? default, midia, _midiaLigada);
        bool visiveis = LadosVisiveis;
        _ladoEsquerdo.Visibility = _ladoDireito.Visibility = visiveis ? Visibility.Visible : Visibility.Collapsed;

        var lado = _lados.Esquerdo;
        _spinner.Visibility = lado == LadoEsquerdo.Rodando ? Visibility.Visible : Visibility.Collapsed;
        _pontoEspera.Visibility = lado == LadoEsquerdo.Esperando ? Visibility.Visible : Visibility.Collapsed;
        _check.Visibility = lado == LadoEsquerdo.Terminou ? Visibility.Visible : Visibility.Collapsed;
        _numero.Visibility = lado is LadoEsquerdo.Rodando or LadoEsquerdo.Esperando ? Visibility.Visible : Visibility.Collapsed;
        _numero.Text = _lados.Numero.ToString(CultureInfo.InvariantCulture);
        if (lado == LadoEsquerdo.Esperando) _numero.SetResourceReference(TextBlock.ForegroundProperty, "Ambar");
        else _numero.Foreground = CorClaude;

        bool girar = visiveis && lado == LadoEsquerdo.Rodando && Movimento.Ligado;
        if (girar) _tiqueSpinner.Start();
        else { _tiqueSpinner.Stop(); _spinner.Text = "✻"; }

        LerCapa(midia?.Capa);
        var fundoCapa = _imagemCapa is null ? null : new ImageBrush(_imagemCapa) { Stretch = Stretch.UniformToFill };
        _capaPequena.Background = _capaEsquerda.Background = fundoCapa;
        _capaPequena.Visibility = _lados.CapaADireita && fundoCapa is not null ? Visibility.Visible : Visibility.Collapsed;
        _capaEsquerda.Visibility = lado == LadoEsquerdo.Capa && fundoCapa is not null ? Visibility.Visible : Visibility.Collapsed;
        foreach (var b in _barras)
        {
            b.Fill = _corBarras;
            b.Visibility = _lados.Barras ? Visibility.Visible : Visibility.Hidden; // o lado fica, para o relógio seguir no centro
        }
        AtualizarCartao();
        AtualizarCaptura();
    }

    /// A captura só corre com música tocando, ligada no perfil, o notch visível e o movimento ligado.
    private void AtualizarCaptura()
    {
        bool tocando = _midia?.Atual?.Tocando == true;
        bool capturar = IsVisible && _lados.Barras && tocando && Movimento.Ligado; // na prévia do Spotify, sem barras
        if (capturar)
        {
            if (_captura is null)
            {
                _captura = new CapturaSom(Dispatcher);
                _captura.Bandas += MostrarBandas;
            }
            _captura.Ligar();
            return;
        }
        _captura?.Desligar();
        double parada = tocando ? BarraParada : BarraMinima; // pausada: baixas; sem movimento: no meio
        foreach (var b in _barras.Concat(_barrasCartao)) b.Height = parada;
    }

    private void MostrarBandas(IReadOnlyList<double> bandas)
    {
        var valores = _somOuDanca.Proximos(bandas, DateTime.Now);
        for (int i = 0; i < _barras.Length && i < valores.Count; i++)
            _barras[i].Height = _barrasCartao[i].Height = BarraMinima + valores[i] * (BarraMaxima - BarraMinima);
    }

    /// A capa e a cor média dela (lidas uma vez por capa).
    private void LerCapa(byte[]? capa)
    {
        if (ReferenceEquals(capa, _capaLida)) return;
        _capaLida = capa;
        _imagemCapa = null;
        _corBarras = CinzaBarras();
        if (capa is not { Length: > 0 }) return;
        try
        {
            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.DecodePixelWidth = 92; // a capa maior do cartão (46 px em escala 2)
            imagem.StreamSource = new System.IO.MemoryStream(capa);
            imagem.EndInit();
            imagem.Freeze();
            _imagemCapa = imagem;
            var pequena = new FormatConvertedBitmap(new TransformedBitmap(imagem, new ScaleTransform(16.0 / imagem.PixelWidth, 16.0 / imagem.PixelHeight)), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[pequena.PixelWidth * pequena.PixelHeight * 4];
            pequena.CopyPixels(pixels, pequena.PixelWidth * 4, 0);
            var (r, g, b) = CorDaCapa.Clarear(CorDaCapa.Media(pixels));
            _corBarras = Congelado(new SolidColorBrush(Color.FromRgb(r, g, b)));
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("JanelaNotch.Capa", e); }
    }

    private Brush CinzaBarras() => TryFindResource("TextoSecundario") as Brush ?? Brushes.Gray;
}

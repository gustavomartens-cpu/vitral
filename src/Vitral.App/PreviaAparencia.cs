using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Vitral.App.Tema;
using Vitral.Core.Aparencia;

namespace Vitral.App;

/// Prévia da aparência na aba Aparência (spec da aparência §5.4): ilhas, notch e doca desenhados com os valores da
/// escolha atual sobre o papel de parede, desfocado sob as partes em vidro. Sem papel de parede legível, um gradiente
/// escuro. Pequena (miniatura da grade de temas), desenha só as formas; sem valores ("Não mexer"), só o aviso.
public sealed class PreviaAparencia : FrameworkElement
{
    public static readonly DependencyProperty ValoresProperty = DependencyProperty.Register(nameof(Valores),
        typeof(ValoresAparencia), typeof(PreviaAparencia), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RaioProperty = DependencyProperty.Register(nameof(Raio),
        typeof(double), typeof(PreviaAparencia), new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public PreviaAparencia()
    {
        // Com SemValoresUsaAtual, desenha a aparência em vigor: redesenha quando ela muda (enquanto está na tela).
        Action redesenhar = InvalidateVisual;
        Loaded += (_, _) => ServicoAparencia.Instancia.Mudou += redesenhar;
        Unloaded += (_, _) => ServicoAparencia.Instancia.Mudou -= redesenhar;
    }

    public static readonly DependencyProperty FormasProperty = DependencyProperty.Register(nameof(Formas),
        typeof(Core.Formas.ValoresFormas), typeof(PreviaAparencia), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SemValoresUsaAtualProperty = DependencyProperty.Register(nameof(SemValoresUsaAtual),
        typeof(bool), typeof(PreviaAparencia), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// As formas desenhadas; nulas = as de hoje.
    public Core.Formas.ValoresFormas? Formas
    {
        get => (Core.Formas.ValoresFormas?)GetValue(FormasProperty);
        set => SetValue(FormasProperty, value);
    }

    /// Aba Formas: com a aparência em "Não mexer", desenha com a aparência em vigor em vez do aviso.
    public bool SemValoresUsaAtual
    {
        get => (bool)GetValue(SemValoresUsaAtualProperty);
        set => SetValue(SemValoresUsaAtualProperty, value);
    }

    public ValoresAparencia? Valores
    {
        get => (ValoresAparencia?)GetValue(ValoresProperty);
        set => SetValue(ValoresProperty, value);
    }

    /// Raio dos cantos da própria prévia.
    public double Raio
    {
        get => (double)GetValue(RaioProperty);
        set => SetValue(RaioProperty, value);
    }

    // Largura e altura de referência do desenho: as partes são posicionadas nessa escala.
    private const double LarguraBase = 420, AlturaBase = 200;
    private const int LarguraFundo = 640, AlturaFundo = 360;

    private static readonly Color[] CoresIcones =
        [Color.FromRgb(0x42, 0x85, 0xF4), Color.FromRgb(0xE8, 0xA3, 0x3A), Color.FromRgb(0x34, 0xA8, 0x53), Color.FromRgb(0x9B, 0x59, 0xB6), Color.FromRgb(0xD9, 0x77, 0x57)];

    // O papel de parede nítido e desfocado, já cortados em 16:9: lidos uma vez e divididos por todas as prévias.
    private static (ImageSource Nitido, ImageSource Desfocado)? _fundo;
    private static bool _fundoLido;

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 1 || h < 1) return;
        var area = new Rect(0, 0, w, h);
        dc.PushClip(new RectangleGeometry(area, Raio, Raio));
        var fundo = Fundo();
        Rect cheio = Preencher(area);
        if (fundo is { } f) dc.DrawImage(f.Nitido, cheio);
        else dc.DrawRectangle(Gradiente(), null, area);

        // Na miniatura as partes ficam proporcionalmente maiores, para o tema ainda se reconhecer.
        bool detalhe = w >= 200;
        double s = detalhe ? Math.Min(w / LarguraBase, h / AlturaBase) : Math.Min(w / 260, h / 125);
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if ((Valores ?? (SemValoresUsaAtual ? ServicoAparencia.Instancia.Atual : null)) is not { } v)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x8C, 0x10, 0x12, 0x18)), null, area);
            var aviso = Texto(detalhe ? "Não mexer: o Vitral fica com a aparência que estiver" : "fica como está",
                detalhe ? 12 : 9, Brushes.White, dip);
            aviso.MaxTextWidth = Math.Max(1, w - 12);
            aviso.TextAlignment = TextAlignment.Center;
            dc.DrawText(aviso, new Point(6, (h - aviso.Height) / 2));
            dc.Pop();
            return;
        }

        // Ilhas nas pontas do topo (ou a barra única), o notch no meio, a doca embaixo: medidas das formas (spec das
        // formas §5.4) reduzidas à escala da prévia.
        var fo = Formas ?? Ui.Barra.PosicaoIlhas.Padrao;
        const double k = 0.8, kDoca = 0.73;
        double larguraIlha = 120 * s, alturaIlha = fo.Ilhas.Altura * k * s, topoIlha = fo.Ilhas.Topo * k * s, margem = fo.Ilhas.Borda * k * s;
        var ilhaEsquerda = new Rect(margem, topoIlha, larguraIlha, alturaIlha);
        var ilhaDireita = new Rect(w - margem - larguraIlha, topoIlha, larguraIlha, alturaIlha);
        double raioIlha = fo.Ilhas.CantosRetos ? 0 : v.Ilhas.RaioPx * s;
        if (fo.Ilhas.Disposicao == Core.Perfis.DisposicaoIlhas.BarraUnica)
            Peca(dc, v.Ilhas, Arredondado(new Rect(margem, topoIlha, w - 2 * margem, alturaIlha), raioIlha, raioIlha, raioIlha, raioIlha), fundo, cheio);
        else
        {
            Peca(dc, v.Ilhas, Arredondado(ilhaEsquerda, raioIlha, raioIlha, raioIlha, raioIlha), fundo, cheio);
            Peca(dc, v.Ilhas, Arredondado(ilhaDireita, raioIlha, raioIlha, raioIlha, raioIlha), fundo, cheio);
        }

        // O notch com o mesmo desenho da tela (orelhas, ilha), na escala da prévia.
        var fn = fo.Notch;
        double larguraNotch = 132 * s, alturaNotch = fn.Altura * k * s, orelha = fn.Orelha * k * s;
        var notch = new Rect(w / 2 - larguraNotch / 2, fn.Afastamento * k * s, larguraNotch, alturaNotch);
        var formaNotch = Geometry.Parse(Ui.Barra.FormaNotch.Caminho(larguraNotch, alturaNotch, fn.Raio * k * s, orelha, fn.CantosDeCima)).CloneCurrentValue();
        formaNotch.Transform = new TranslateTransform(notch.X - orelha, notch.Y);
        Peca(dc, v.Notch, formaNotch, fundo, cheio);

        var fd = fo.Doca;
        double icone = fd.Icone * kDoca * s, vao = 7 * s, folga = 9 * s, alturaDoca = fd.Altura * kDoca * s;
        double larguraIcones = 5 * icone + 4 * vao;
        var doca = fd.Formato == Core.Perfis.FormatoDoca.Corrida
            ? new Rect(0, h - alturaDoca, w, alturaDoca)
            : new Rect(w / 2 - larguraIcones / 2 - folga, h - 10 * s - alturaDoca, larguraIcones + 2 * folga, alturaDoca);
        double raioDoca = fd.CantosRetos ? 0 : v.Doca.RaioPx * 1.5 * s;
        Peca(dc, v.Doca, Arredondado(doca, raioDoca, raioDoca, raioDoca, raioDoca), fundo, cheio);

        double xIcones = w / 2 - larguraIcones / 2;
        for (int i = 0; i < CoresIcones.Length; i++)
        {
            var r = new Rect(xIcones + i * (icone + vao), doca.Y + (alturaDoca - icone) / 2, icone, icone);
            dc.DrawRoundedRectangle(new SolidColorBrush(CoresIcones[i]), null, r, icone * 0.27, icone * 0.27);
        }
        // O app em foco: a marca embaixo do segundo ícone, na cor de destaque da doca.
        var marca = new Rect(xIcones + icone + vao + icone / 2 - 5 * s, doca.Bottom - 4 * s, 10 * s, Math.Max(1, 2 * s));
        dc.DrawRoundedRectangle(ServicoAparencia.Pincel(v.Doca.Destaque), null, marca, s, s);

        double ponto = Math.Max(2, 6 * s);
        if (detalhe)
        {
            double tamanho = 11 * s, tamanhoNotch = tamanho * fn.EscalaTexto;
            var musica = Texto("♪  Música tocando", tamanho, ServicoAparencia.Pincel(v.Ilhas.Texto), dip);
            dc.DrawText(musica, new Point(ilhaEsquerda.X + 9 * s, ilhaEsquerda.Y + (alturaIlha - musica.Height) / 2));
            var tocar = Texto("▶", tamanho, ServicoAparencia.Pincel(v.Ilhas.Destaque), dip);
            dc.DrawText(tocar, new Point(ilhaEsquerda.Right - 9 * s - tocar.Width, ilhaEsquerda.Y + (alturaIlha - tocar.Height) / 2));

            var cpu = Texto("CPU 12%", tamanho, ServicoAparencia.Pincel(v.Ilhas.TextoSecundario), dip);
            var bateria = Texto("82%", tamanho, ServicoAparencia.Pincel(v.Ilhas.Texto), dip);
            double xCpu = ilhaDireita.X + (larguraIlha - cpu.Width - 12 * s - bateria.Width) / 2;
            dc.DrawText(cpu, new Point(xCpu, ilhaDireita.Y + (alturaIlha - cpu.Height) / 2));
            dc.DrawText(bateria, new Point(xCpu + cpu.Width + 12 * s, ilhaDireita.Y + (alturaIlha - bateria.Height) / 2));

            var hora = Texto(DateTime.Now.ToString("HH:mm · ddd, d MMM", CultureInfo.GetCultureInfo("pt-BR")), tamanhoNotch,
                ServicoAparencia.Pincel(v.Notch.Texto), dip, FontWeights.SemiBold);
            double xHora = notch.X + (notch.Width - hora.Width - ponto - 6 * s) / 2;
            dc.DrawText(hora, new Point(xHora, notch.Y + (notch.Height - hora.Height) / 2));
            dc.DrawEllipse(ServicoAparencia.Pincel(v.Notch.Destaque), null,
                new Point(xHora + hora.Width + 6 * s + ponto / 2, notch.Y + notch.Height / 2), ponto / 2, ponto / 2);
        }
        else
        {
            dc.DrawEllipse(ServicoAparencia.Pincel(v.Notch.Destaque), null, new Point(notch.X + notch.Width / 2, notch.Y + notch.Height / 2), ponto / 2, ponto / 2);
            dc.DrawEllipse(ServicoAparencia.Pincel(v.Ilhas.Destaque), null, new Point(ilhaEsquerda.Right - 8 * s, ilhaEsquerda.Y + alturaIlha / 2), ponto / 2, ponto / 2);
        }
        dc.Pop();
    }

    /// Uma parte: o desfoque do papel de parede embaixo (vidro), o fundo da parte por cima e a borda.
    private static void Peca(DrawingContext dc, ValoresParte v, Geometry forma, (ImageSource Nitido, ImageSource Desfocado)? fundo, Rect cheio)
    {
        if (v.Desfoque)
        {
            dc.PushClip(forma);
            if (fundo is { } f) dc.DrawImage(f.Desfocado, cheio);
            dc.Pop();
        }
        dc.DrawGeometry(ServicoAparencia.Pincel(v.Fundo), new Pen(ServicoAparencia.Pincel(v.Borda), 1), forma);
    }

    private FormattedText Texto(string texto, double tamanho, Brush cor, double dip, FontWeight? peso = null) =>
        new(texto, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, peso ?? FontWeights.Normal, FontStretches.Normal),
            Math.Max(1, tamanho), cor, dip);

    private static Geometry Arredondado(Rect r, double se, double sd, double id, double ie)
    {
        double max = Math.Min(r.Width, r.Height) / 2;
        se = Math.Min(se, max); sd = Math.Min(sd, max); id = Math.Min(id, max); ie = Math.Min(ie, max);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(r.Left + se, r.Top), true, true);
            c.LineTo(new Point(r.Right - sd, r.Top), true, false);
            if (sd > 0) c.ArcTo(new Point(r.Right, r.Top + sd), new Size(sd, sd), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(r.Right, r.Bottom - id), true, false);
            if (id > 0) c.ArcTo(new Point(r.Right - id, r.Bottom), new Size(id, id), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(r.Left + ie, r.Bottom), true, false);
            if (ie > 0) c.ArcTo(new Point(r.Left, r.Bottom - ie), new Size(ie, ie), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(r.Left, r.Top + se), true, false);
            if (se > 0) c.ArcTo(new Point(r.Left + se, r.Top), new Size(se, se), 0, false, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    /// O retângulo 16:9 que cobre a área toda (corta as sobras, como o papel de parede em "Preencher").
    private static Rect Preencher(Rect area)
    {
        double escala = Math.Max(area.Width / LarguraFundo, area.Height / AlturaFundo);
        double lw = LarguraFundo * escala, lh = AlturaFundo * escala;
        return new Rect((area.Width - lw) / 2, (area.Height - lh) / 2, lw, lh);
    }

    private static Brush Gradiente()
    {
        var g = new RadialGradientBrush { GradientOrigin = new Point(0.3, 0.3), Center = new Point(0.3, 0.3), RadiusX = 0.9, RadiusY = 1.2 };
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0x2A, 0x34, 0x5C), 0));
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0x16, 0x1A, 0x2C), 0.45));
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0x0B, 0x0C, 0x12), 1));
        g.Freeze();
        return g;
    }

    private static (ImageSource Nitido, ImageSource Desfocado)? Fundo()
    {
        if (_fundoLido) return _fundo;
        _fundoLido = true;
        try
        {
            using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            string? caminho = desktop?.GetValue("WallPaper") as string;
            if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho)) return _fundo = null;
            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.UriSource = new Uri(caminho);
            imagem.DecodePixelWidth = LarguraFundo * 2;
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            imagem.EndInit();
            imagem.Freeze();
            BitmapSource origem = imagem;
            // "Estender" (22): uma imagem só cobre todos os monitores; a prévia mostra o pedaço do monitor principal.
            if (desktop?.GetValue("WallpaperStyle") as string == "22" && SystemParameters.VirtualScreenWidth > 0)
            {
                double ex = imagem.PixelWidth / SystemParameters.VirtualScreenWidth, ey = imagem.PixelHeight / SystemParameters.VirtualScreenHeight;
                var recorte = new Int32Rect((int)(-SystemParameters.VirtualScreenLeft * ex), (int)(-SystemParameters.VirtualScreenTop * ey),
                    (int)(SystemParameters.PrimaryScreenWidth * ex), (int)(SystemParameters.PrimaryScreenHeight * ey));
                if (recorte.X >= 0 && recorte.Y >= 0 && recorte.Width > 0 && recorte.Height > 0
                    && recorte.X + recorte.Width <= imagem.PixelWidth && recorte.Y + recorte.Height <= imagem.PixelHeight)
                {
                    var cortada = new CroppedBitmap(imagem, recorte);
                    cortada.Freeze();
                    origem = cortada;
                }
            }
            _fundo = (Cortar(origem, 0), Cortar(origem, 22));
        }
        catch (Exception) // papel de parede ilegível (formato, permissão): fica o gradiente
        {
            _fundo = null;
        }
        return _fundo;
    }

    /// O papel de parede cortado em 16:9 e, com raio, desfocado (a imagem passa um pouco das bordas para o desfoque não
    /// escurecer os cantos).
    private static ImageSource Cortar(BitmapSource origem, double raio)
    {
        const double sobra = 32;
        var imagem = new Image { Source = origem, Stretch = Stretch.UniformToFill, Margin = new Thickness(-sobra) };
        if (raio > 0) imagem.Effect = new BlurEffect { Radius = raio, KernelType = KernelType.Gaussian };
        var caixa = new Grid { Width = LarguraFundo, Height = AlturaFundo, ClipToBounds = true };
        caixa.Children.Add(imagem);
        caixa.Measure(new Size(LarguraFundo, AlturaFundo));
        caixa.Arrange(new Rect(0, 0, LarguraFundo, AlturaFundo));
        caixa.UpdateLayout();
        var bitmap = new RenderTargetBitmap(LarguraFundo, AlturaFundo, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(caixa);
        bitmap.Freeze();
        return bitmap;
    }
}

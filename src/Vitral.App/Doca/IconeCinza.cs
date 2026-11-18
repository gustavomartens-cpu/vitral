using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vitral.App.Barra;
using Vitral.Core.Formas;

namespace Vitral.App.Doca;

/// A cópia em tons de cinza do ícone de um app (spec dos ícones em preto e branco, 17/11/2026). Valores: o ícone e se
/// a doca está em preto e branco. Desligado ou sem ícone: nulo (a camada cinza fica vazia e não se converte nada).
/// Cada ícone é convertido uma vez e guardado num cache fraco: some junto com o ícone (janela fechada). Qualquer falha
/// devolve o original, para o ícone nunca sumir.
internal sealed class IconeCinza : IMultiValueConverter
{
    private static readonly ConditionalWeakTable<ImageSource, ImageSource> Cache = [];

    public object? Convert(object[] valores, Type alvo, object? parametro, CultureInfo cultura)
    {
        if (valores.Length < 2 || valores[1] is not true || valores[0] is not ImageSource icone) return null;
        return Cache.GetValue(icone, Gerar);
    }

    public object[] ConvertBack(object? valor, Type[] alvos, object? parametro, CultureInfo cultura) => throw new NotSupportedException();

    private static ImageSource Gerar(ImageSource icone)
    {
        try
        {
            var bgra = Pbgra32(icone);
            if (bgra is null) return icone;
            int largura = bgra.PixelWidth, altura = bgra.PixelHeight, passo = largura * 4;
            var pixels = new byte[passo * altura];
            bgra.CopyPixels(pixels, passo, 0);
            TonsDeCinza.Converter(pixels);
            var cinza = BitmapSource.Create(largura, altura, bgra.DpiX, bgra.DpiY, PixelFormats.Pbgra32, null, pixels, passo);
            cinza.Freeze();
            return cinza;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("IconeCinza", e);
            return icone;
        }
    }

    /// O ícone em BGRA pré-multiplicado; um desenho (não bitmap) é rasterizado no próprio tamanho. Nulo se vazio.
    private static BitmapSource? Pbgra32(ImageSource icone)
    {
        if (icone is BitmapSource b)
        {
            if (b.PixelWidth <= 0 || b.PixelHeight <= 0) return null;
            return b.Format == PixelFormats.Pbgra32 ? b : new FormatConvertedBitmap(b, PixelFormats.Pbgra32, null, 0);
        }
        int l = (int)Math.Ceiling(icone.Width), a = (int)Math.Ceiling(icone.Height);
        if (l <= 0 || a <= 0) return null;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawImage(icone, new Rect(0, 0, l, a));
        var rt = new RenderTargetBitmap(l, a, 96, 96, PixelFormats.Pbgra32);
        rt.Render(visual);
        return rt;
    }
}

/// A opacidade da imagem colorida: valores = a fonte da camada cinza e a opacidade dela (CamadasIcone).
internal sealed class OpacidadeOriginal : IMultiValueConverter
{
    public object Convert(object[] valores, Type alvo, object? parametro, CultureInfo cultura) =>
        CamadasIcone.OpacidadeOriginal(valores.Length > 0 && valores[0] is ImageSource, valores.Length > 1 && valores[1] is double o ? o : 1);

    public object[] ConvertBack(object? valor, Type[] alvos, object? parametro, CultureInfo cultura) => throw new NotSupportedException();
}

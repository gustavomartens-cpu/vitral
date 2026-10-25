using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Ícone (Segoe Fluent Icons) do tipo de rede.
internal sealed class RedeParaIcone : IValueConverter
{
    public object Convert(object? valor, Type alvo, object? parametro, CultureInfo cultura) => valor switch
    {
        TipoRede.Wifi => "",
        TipoRede.Cabo => "",
        _ => "", // sem conexão
    };

    public object ConvertBack(object? valor, Type alvo, object? parametro, CultureInfo cultura) => throw new NotSupportedException();
}

/// Ícone de sinal Wi-Fi (Segoe Fluent Icons) pelas barras, de 0 a 4.
internal sealed class SinalParaIcone : IValueConverter
{
    public object Convert(object? valor, Type alvo, object? parametro, CultureInfo cultura) => valor switch
    {
        >= 4 => "",
        3 => "",
        2 => "",
        _ => "",
    };

    public object ConvertBack(object? valor, Type alvo, object? parametro, CultureInfo cultura) => throw new NotSupportedException();
}

/// Largura do preenchimento de um controle deslizante: valor / máximo × largura disponível.
internal sealed class LarguraPreenchida : IMultiValueConverter
{
    public object Convert(object[] valores, Type alvo, object? parametro, CultureInfo cultura)
    {
        if (valores is [double valor, double maximo, double largura] && maximo > 0)
            return Math.Clamp(valor / maximo, 0, 1) * largura;
        return 0.0;
    }

    public object[] ConvertBack(object? valor, Type[] alvos, object? parametro, CultureInfo cultura) => throw new NotSupportedException();
}

/// Bytes da capa do álbum para imagem; null ou inválido vira null (a ilha mostra o ícone de música).
internal sealed class BytesParaImagem : IValueConverter
{
    public object? Convert(object? valor, Type alvo, object? parametro, CultureInfo cultura)
    {
        if (valor is not byte[] { Length: > 0 } bytes) return null;
        try
        {
            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.DecodePixelWidth = parametro is string s && int.TryParse(s, out int largura) ? largura : 36; // ilha 36, painel maior
            imagem.StreamSource = new MemoryStream(bytes);
            imagem.EndInit();
            imagem.Freeze();
            return imagem;
        }
        catch (Exception) { return null; }
    }

    public object ConvertBack(object? valor, Type alvo, object? parametro, CultureInfo cultura) => throw new NotSupportedException();
}

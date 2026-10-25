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
            imagem.DecodePixelWidth = 36;
            imagem.StreamSource = new MemoryStream(bytes);
            imagem.EndInit();
            imagem.Freeze();
            return imagem;
        }
        catch (Exception) { return null; }
    }

    public object ConvertBack(object? valor, Type alvo, object? parametro, CultureInfo cultura) => throw new NotSupportedException();
}

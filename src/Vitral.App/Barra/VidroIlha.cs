using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vitral.App.Barra;

/// Fundo das ilhas. A técnica sai do teste de vidro (plano da barra do topo, Task 1); até lá, o fallback
/// da spec §3.4: Noite a 85%, sem desfoque, numa janela em camadas (para o raio de pílula).
internal static class VidroIlha
{
    private static readonly Color Noite85 = Color.FromArgb(0xD9, 0x0A, 0x0F, 0x20);

    /// Chamado antes de a janela ser mostrada.
    public static void Preparar(Window janela, Border casca)
    {
        janela.AllowsTransparency = true;
        janela.Background = Brushes.Transparent;
        casca.Background = new SolidColorBrush(Noite85);
    }
}

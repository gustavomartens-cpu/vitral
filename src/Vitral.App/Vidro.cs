using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Vitral.App;

/// Fundo "Vitral noturno": acrílico do Windows com tinta azul-noite; se a transparência do Windows
/// estiver desligada, azul-noite sólido (spec 2026-10-24 §2.2).
internal static partial class Vidro
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaSystemBackdropType = 38;
    private const int CorTitulo = 0x00200F0A;      // COLORREF 0x00BBGGRR de #0A0F20 (Noite)
    private const int CorTextoTitulo = 0x00F7EFED; // #EDEFF7 (Texto)
    private const int DwmsbtNone = 1;
    private const int DwmsbtTransientWindow = 3; // acrílico
    private const int DwmwcpRound = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Margens { public int Esq, Dir, Topo, Base; }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margens margens);

    public static void Aplicar(Window janela)
    {
        janela.SourceInitialized += (_, _) => Atualizar(janela);
        UserPreferenceChangedEventHandler aoMudar = (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
                janela.Dispatcher.BeginInvoke(() => Atualizar(janela));
        };
        SystemEvents.UserPreferenceChanged += aoMudar;
        janela.Closed += (_, _) => SystemEvents.UserPreferenceChanged -= aoMudar;
    }

    private static void Atualizar(Window janela)
    {
        var hwnd = new WindowInteropHelper(janela).Handle;
        if (hwnd == IntPtr.Zero) return;

        int sim = 1, canto = DwmwcpRound, titulo = CorTitulo, textoTitulo = CorTextoTitulo;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref sim, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref canto, sizeof(int));
        // Barra de título sempre azul-noite, mesmo com "destaque nas barras de título" ligado no Windows
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref titulo, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref textoTitulo, sizeof(int));

        bool transparencia = TransparenciaLigada();
        var fonte = HwndSource.FromHwnd(hwnd);
        if (transparencia)
        {
            if (fonte?.CompositionTarget is { } alvo) alvo.BackgroundColor = Colors.Transparent;
            var m = new Margens { Esq = -1, Dir = -1, Topo = -1, Base = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);
            int acrilico = DwmsbtTransientWindow;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref acrilico, sizeof(int));
            janela.Background = (Brush)Application.Current.Resources["NoiteVidro"];
        }
        else
        {
            // Desfaz o vidro se a transparência foi desligada com a janela aberta
            if (fonte?.CompositionTarget is { } alvo) alvo.BackgroundColor = (Color)Application.Current.Resources["CorNoite"];
            var m = new Margens();
            DwmExtendFrameIntoClientArea(hwnd, ref m);
            int nenhum = DwmsbtNone;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref nenhum, sizeof(int));
            janela.Background = (Brush)Application.Current.Resources["Noite"];
        }
    }

    private static bool TransparenciaLigada()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return k?.GetValue("EnableTransparency") is int v && v != 0;
    }
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Vitral.App.Barra;

/// Fundo das ilhas, decidido no teste de vidro (plano da barra do topo, Task 1): acrílico por
/// SetWindowCompositionAttribute em janela sem camadas, a única técnica que manteve o desfoque com a janela
/// inativa. Os cantos são os do DWM (~8 px). Com a transparência do Windows desligada: Noite a 85%, sólido.
internal static partial class VidroIlha
{
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0, AccentAcrylicBlurBehind = 4;
    private const int DwmwaUseImmersiveDarkMode = 20, DwmwaWindowCornerPreference = 33, DwmwcpRound = 2;
    private const uint TintaNoiteVidro = 0xB8200F0A; // ABGR de #B80A0F20 (NoiteVidro)
    private static readonly Color Noite85 = Color.FromArgb(0xD9, 0x0A, 0x0F, 0x20);

    [StructLayout(LayoutKind.Sequential)] private struct Accent { public int Estado, Flags; public uint Cor; public int Animacao; }
    [StructLayout(LayoutKind.Sequential)] private struct DadosComposicao { public int Atributo; public IntPtr Dados; public int Tamanho; }
    [StructLayout(LayoutKind.Sequential)] private struct Margens { public int Esq, Dir, Topo, Base; }

    [LibraryImport("user32.dll")] private static partial int SetWindowCompositionAttribute(IntPtr janela, ref DadosComposicao dados);
    [LibraryImport("dwmapi.dll")] private static partial int DwmSetWindowAttribute(IntPtr janela, int atributo, ref int valor, int tamanho);
    [LibraryImport("dwmapi.dll")] private static partial int DwmExtendFrameIntoClientArea(IntPtr janela, ref Margens margens);

    /// Chamado no construtor da ilha, antes de ela ser mostrada.
    public static void Preparar(Window janela, Border casca)
    {
        janela.AllowsTransparency = false;
        janela.Background = Brushes.Transparent;
        casca.CornerRadius = new CornerRadius(8);
        janela.SourceInitialized += (_, _) => Atualizar(janela, casca);
    }

    /// Reaplica (por exemplo quando a transparência do Windows muda).
    public static void Atualizar(Window janela, Border casca)
    {
        var h = new WindowInteropHelper(janela).Handle;
        if (h == IntPtr.Zero) return;
        int sim = 1, canto = DwmwcpRound;
        DwmSetWindowAttribute(h, DwmwaUseImmersiveDarkMode, ref sim, sizeof(int));
        DwmSetWindowAttribute(h, DwmwaWindowCornerPreference, ref canto, sizeof(int));

        bool vidro = Vidro.TransparenciaLigada();
        if (HwndSource.FromHwnd(h)?.CompositionTarget is { } alvo) alvo.BackgroundColor = vidro ? Colors.Transparent : Noite85;
        var m = vidro ? new Margens { Esq = -1, Dir = -1, Topo = -1, Base = -1 } : new Margens();
        DwmExtendFrameIntoClientArea(h, ref m);
        DefinirAcento(h, vidro ? AccentAcrylicBlurBehind : AccentDisabled);
        casca.Background = vidro ? Brushes.Transparent : new SolidColorBrush(Noite85);
    }

    private static void DefinirAcento(IntPtr h, int estado)
    {
        var acento = new Accent { Estado = estado, Flags = 2, Cor = TintaNoiteVidro };
        int tamanho = Marshal.SizeOf<Accent>();
        IntPtr ptr = Marshal.AllocHGlobal(tamanho);
        try
        {
            Marshal.StructureToPtr(acento, ptr, false);
            var dados = new DadosComposicao { Atributo = WcaAccentPolicy, Dados = ptr, Tamanho = tamanho };
            SetWindowCompositionAttribute(h, ref dados);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}

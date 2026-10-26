using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Posição das janelas em pixels físicos, pela HWND. Com monitores em escalas diferentes, o WPF cria a janela na escala
/// do sistema (a do monitor do login) e converte o Left/Top por ela: a ilha de status nascia no notebook a 125% em vez
/// do principal a 100%, e o notch ficava com a HWND 1,25× maior que o Width e centrado 59 px à direita (26/10). Aqui o
/// alvo vem em DIPs do monitor (pela escala dele) e a largura da janela, pela escala que o WPF dá a ela.
internal static partial class PosicaoNativa
{
    private const uint SwpNoSize = 0x1, SwpNoZOrder = 0x4, SwpNoActivate = 0x10;

    [StructLayout(LayoutKind.Sequential)] private struct Ret { public int Esq, Topo, Dir, Base; }

    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr janela, out Ret ret);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr janela, IntPtr depoisDe, int x, int y, int largura, int altura, uint flags);

    private static double Escala(uint dpi) => dpi == 0 ? 1 : dpi / 96.0;

    /// Põe o ponto (x, y), em DIPs do monitor de escala dpi, na fração ancora da largura da janela (0 = borda esquerda,
    /// 0,5 = centro, 1 = borda direita).
    public static void Colocar(Window janela, double x, double y, uint dpi, double ancora = 0)
    {
        var h = new WindowInteropHelper(janela).EnsureHandle();
        double largura = janela.ActualWidth > 0 ? janela.ActualWidth : double.IsNaN(janela.Width) ? 0 : janela.Width;
        double larguraPx = largura * VisualTreeHelper.GetDpi(janela).DpiScaleX;
        double escala = Escala(dpi);
        SetWindowPos(h, IntPtr.Zero, (int)Math.Round(x * escala - ancora * larguraPx), (int)Math.Round(y * escala), 0, 0,
            SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    /// Onde a janela está, em DIPs do monitor de escala dpi (pela HWND, não pelo Left/Top do WPF).
    public static Retangulo Ler(Window janela, uint dpi)
    {
        var h = new WindowInteropHelper(janela).Handle;
        if (h == IntPtr.Zero || !GetWindowRect(h, out var r))
            return new Retangulo(janela.Left, janela.Top, janela.ActualWidth, janela.ActualHeight);
        double escala = Escala(dpi);
        return new Retangulo(r.Esq / escala, r.Topo / escala, (r.Dir - r.Esq) / escala, (r.Base - r.Topo) / escala);
    }
}

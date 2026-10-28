using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Vitral.App.Tema;
using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.App.Barra;

/// Fundo das ilhas, dos painéis e da doca, decidido no teste de vidro (plano da barra do topo, Task 1): acrílico por
/// SetWindowCompositionAttribute em janela sem camadas, a única técnica que manteve o desfoque com a janela inativa.
/// Cor, material e cantos vêm da aparência da parte (spec da aparência §5.2): vidro → acrílico tingido; sólido → fundo
/// opaco; cantos pelo DWM (retos, pequenos ou padrão).
internal static partial class VidroIlha
{
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0, AccentAcrylicBlurBehind = 4;
    private const int DwmwaUseImmersiveDarkMode = 20, DwmwaWindowCornerPreference = 33;
    private const int DwmwcpNaoArredondar = 1, DwmwcpRound = 2, DwmwcpRoundSmall = 3;

    [StructLayout(LayoutKind.Sequential)] private struct Accent { public int Estado, Flags; public uint Cor; public int Animacao; }
    [StructLayout(LayoutKind.Sequential)] private struct DadosComposicao { public int Atributo; public IntPtr Dados; public int Tamanho; }
    [StructLayout(LayoutKind.Sequential)] private struct Margens { public int Esq, Dir, Topo, Base; }

    [LibraryImport("user32.dll")] private static partial int SetWindowCompositionAttribute(IntPtr janela, ref DadosComposicao dados);
    [LibraryImport("dwmapi.dll")] private static partial int DwmSetWindowAttribute(IntPtr janela, int atributo, ref int valor, int tamanho);
    [LibraryImport("dwmapi.dll")] private static partial int DwmExtendFrameIntoClientArea(IntPtr janela, ref Margens margens);

    private static readonly DependencyProperty ParteProperty =
        DependencyProperty.RegisterAttached("Parte", typeof(ParteAparencia), typeof(VidroIlha), new PropertyMetadata(ParteAparencia.Ilhas));

    private static readonly DependencyProperty CantosRetosProperty =
        DependencyProperty.RegisterAttached("CantosRetos", typeof(bool), typeof(VidroIlha), new PropertyMetadata(false));

    /// Doca corrida e ilhas encostadas (spec das formas §5.3): o Windows arredonda os quatro cantos ou nenhum, então
    /// nenhum. Vale a partir do próximo <see cref="Atualizar"/>.
    public static void DefinirCantosRetos(Window janela, bool retos) => janela.SetValue(CantosRetosProperty, retos);

    /// Chamado no construtor da janela, antes de ela ser mostrada: a janela passa a seguir a aparência da parte.
    public static void Preparar(Window janela, Border casca, ParteAparencia parte = ParteAparencia.Ilhas)
    {
        janela.AllowsTransparency = false;
        janela.Background = Brushes.Transparent;
        janela.SetValue(ParteProperty, parte);
        var servico = ServicoAparencia.Instancia;
        servico.Registrar(janela, parte);
        janela.SetResourceReference(Control.ForegroundProperty, "Texto");
        casca.SetResourceReference(Border.BorderBrushProperty, "BordaVidro");
        Action aoMudar = () => Atualizar(janela, casca);
        servico.Mudou += aoMudar;
        janela.Closed += (_, _) => servico.Mudou -= aoMudar;
        janela.SourceInitialized += (_, _) => Atualizar(janela, casca);
    }

    /// Reaplica (a aparência ou a transparência do Windows mudou).
    public static void Atualizar(Window janela, Border casca)
    {
        var v = ServicoAparencia.Instancia.Valores((ParteAparencia)janela.GetValue(ParteProperty));
        bool retos = (bool)janela.GetValue(CantosRetosProperty);
        casca.CornerRadius = new CornerRadius(retos ? 0 : v.RaioPx);
        var h = new WindowInteropHelper(janela).Handle;
        if (h == IntPtr.Zero) return;
        int escuro = ServicoAparencia.Luminancia(v.Texto) > 0.5 ? 1 : 0; // texto claro = fundo escuro
        int canto = retos ? DwmwcpNaoArredondar
            : v.Cantos switch { CantosParte.Retos => DwmwcpNaoArredondar, CantosParte.Suaves => DwmwcpRoundSmall, _ => DwmwcpRound };
        DwmSetWindowAttribute(h, DwmwaUseImmersiveDarkMode, ref escuro, sizeof(int));
        DwmSetWindowAttribute(h, DwmwaWindowCornerPreference, ref canto, sizeof(int));

        bool vidro = v.Desfoque;
        var opaco = ServicoAparencia.Cor(v.Fundo with { A = 255 });
        if (HwndSource.FromHwnd(h)?.CompositionTarget is { } alvo) alvo.BackgroundColor = vidro ? Colors.Transparent : opaco;
        var m = vidro ? new Margens { Esq = -1, Dir = -1, Topo = -1, Base = -1 } : new Margens();
        DwmExtendFrameIntoClientArea(h, ref m);
        DefinirAcento(h, vidro ? AccentAcrylicBlurBehind : AccentDisabled, v.Fundo);
        casca.Background = vidro ? Brushes.Transparent : ServicoAparencia.Pincel(v.Fundo with { A = 255 });
    }

    private static void DefinirAcento(IntPtr h, int estado, CorArgb tinta)
    {
        // A tinta do acrílico é ABGR.
        uint cor = (uint)tinta.A << 24 | (uint)tinta.B << 16 | (uint)tinta.G << 8 | tinta.R;
        var acento = new Accent { Estado = estado, Flags = 2, Cor = cor };
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

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Vitral.App.Tema;
using Vitral.Ui.Doca;
using Vitral.Ui.Tema;

namespace Vitral.App.Doca;

/// Menu do botão direito da doca, montado com DocaViewModel.Menu. O visual vem do estilo de menu do tema
/// (Tema/Estilos.xaml); abre subindo da doca.
internal static partial class MenuDoca
{
    [LibraryImport("user32.dll")] private static partial short GetAsyncKeyState(int tecla);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetCursorPos(out Ponto p);
    [StructLayout(LayoutKind.Sequential)] private struct Ponto { public int X, Y; }

    public static void Abrir(UIElement alvo, IReadOnlyList<ItemMenu> itens)
    {
        var menu = new ContextMenu { PlacementTarget = alvo, Placement = PlacementMode.Top };
        foreach (var item in itens)
        {
            if (item.Perigo && menu.Items.Count > 0) menu.Items.Add(new Separator());
            var mi = new MenuItem { Header = item.Texto, Icon = item.Icone };
            Efeitos.SetPerigo(mi, item.Perigo);
            mi.Click += (_, _) => item.Acao();
            menu.Items.Add(mi);
        }
        menu.Opened += (_, _) => Efeitos.Assentar(menu, AssentarDe.Baixo, cascata: false);
        menu.IsOpen = true;
        FecharAoClicarFora(menu);
    }

    /// A doca não rouba o foco, então o Windows não avisa o menu de um clique em outro app: confere os botões do mouse
    /// a cada 50 ms enquanto ele está aberto e fecha num clique novo fora dele.
    private static void FecharAoClicarFora(ContextMenu menu)
    {
        bool antes = AlgumBotao();
        var vigia = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        vigia.Tick += (_, _) =>
        {
            bool agora = AlgumBotao();
            if (MenuFora.DeveFechar(antes, agora, CursorNoMenu(menu))) menu.IsOpen = false;
            antes = agora;
        };
        menu.Closed += (_, _) => vigia.Stop();
        vigia.Start();
    }

    private static bool AlgumBotao() =>
        (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0 || (GetAsyncKeyState(0x04) & 0x8000) != 0;

    private static bool CursorNoMenu(ContextMenu menu)
    {
        if (!menu.IsLoaded || !GetCursorPos(out var p)) return false;
        try
        {
            var canto = menu.PointToScreen(new Point(0, 0));
            var outro = menu.PointToScreen(new Point(menu.ActualWidth, menu.ActualHeight));
            return p.X >= canto.X && p.X <= outro.X && p.Y >= canto.Y && p.Y <= outro.Y;
        }
        catch (InvalidOperationException) { return false; } // popup ainda sem janela
    }
}

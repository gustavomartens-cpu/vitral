using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

/// Menu do botão direito da doca, montado com DocaViewModel.Menu (o visual vem do tema Fluent escuro do app).
internal static class MenuDoca
{
    public static void Abrir(UIElement alvo, IReadOnlyList<ItemMenu> itens)
    {
        var menu = new ContextMenu { PlacementTarget = alvo, Placement = PlacementMode.Top };
        foreach (var item in itens)
        {
            if (item.Texto.StartsWith("Fechar", StringComparison.Ordinal) && menu.Items.Count > 0) menu.Items.Add(new Separator());
            var mi = new MenuItem { Header = item.Texto };
            mi.Click += (_, _) => item.Acao();
            menu.Items.Add(mi);
        }
        menu.IsOpen = true;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

public partial class ConteudoDoca : UserControl
{
    private Point? _inicio;

    public ConteudoDoca() => InitializeComponent();

    private DocaViewModel? Vm => DataContext as DocaViewModel;

    private static BotaoAppViewModel? BotaoDe(object sender) => (sender as FrameworkElement)?.DataContext as BotaoAppViewModel;

    /// Menu do botão direito: fixar ou desafixar (o resto do menu entra com as miniaturas).
    private void Botao_Direito(object sender, MouseButtonEventArgs e)
    {
        if (Vm is not { } vm || BotaoDe(sender) is not { } botao) return;
        var item = botao.Fixado is null
            ? new MenuItem { Header = "Fixar", Command = vm.FixarCommand, CommandParameter = botao }
            : new MenuItem { Header = "Desafixar", Command = vm.DesafixarCommand, CommandParameter = botao };
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        menu.Items.Add(item);
        menu.IsOpen = true;
        e.Handled = true;
    }

    // Arrastar para reordenar: só fixados, e só para a posição de outro fixado (DocaViewModel.Mover confere).
    private void Botao_Apertou(object sender, MouseButtonEventArgs e) => _inicio = e.GetPosition(this);

    private void Botao_Moveu(object sender, MouseEventArgs e)
    {
        if (_inicio is not { } inicio || e.LeftButton != MouseButtonState.Pressed || BotaoDe(sender) is not { Fixado: not null } botao) return;
        var d = e.GetPosition(this) - inicio;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _inicio = null;
        DragDrop.DoDragDrop((DependencyObject)sender, botao, DragDropEffects.Move);
    }

    private void Botao_Soltou(object sender, DragEventArgs e)
    {
        if (Vm is not { } vm || e.Data.GetData(typeof(BotaoAppViewModel)) is not BotaoAppViewModel arrastado || BotaoDe(sender) is not { } alvo) return;
        vm.Mover(vm.Botoes.IndexOf(arrastado), vm.Botoes.IndexOf(alvo));
    }
}

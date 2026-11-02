using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// O painel que desce do sino do notch (spec das notificações §6): agrupado por app, com limpar (um, o app, tudo), abrir o
/// app e a central do Windows. Sem identidade ou sem permissão, explica e oferece a central.
internal sealed class PainelNotificacoes : StackPanel
{
    private readonly PainelNotificacoesViewModel _vm;
    private readonly Func<string, Task<ImageSource?>> _icone;

    public PainelNotificacoes(PainelNotificacoesViewModel vm, Func<string, Task<ImageSource?>> icone)
    {
        _vm = vm;
        _icone = icone;
        _vm.PropertyChanged += AoMudar;
        Montar();
    }

    private void AoMudar(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PainelNotificacoesViewModel.Grupos)) Montar();
    }

    private void Montar()
    {
        Children.Clear();
        if (_vm.TextoEstado is { } estado)
        {
            Children.Add(Texto(estado, secundario: false, quebra: true, margem: new Thickness(8, 4, 8, 8)));
            Children.Add(Rodape(tudo: false));
            return;
        }
        if (_vm.Vazio)
        {
            Children.Add(Texto("Nenhuma notificação.", secundario: true, quebra: false, margem: new Thickness(8)));
            Children.Add(Rodape(tudo: false));
            return;
        }
        var lista = new StackPanel();
        foreach (var g in _vm.Grupos) lista.Children.Add(Grupo(g));
        Children.Add(new ScrollViewer { MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = lista });
        Children.Add(Rodape(tudo: true));
    }

    private UIElement Grupo(GrupoPainelNotificacao g)
    {
        var cabeca = new DockPanel { Margin = new Thickness(8, 8, 8, 2) };
        var icone = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        _ = Preencher(icone, g.AppId);
        DockPanel.SetDock(icone, Dock.Left);
        var limpar = Link("Limpar", () => _vm.LimparAppCommand.Execute(g));
        DockPanel.SetDock(limpar, Dock.Right);
        cabeca.Children.Add(icone);
        cabeca.Children.Add(limpar);
        var nome = Texto(g.NomeApp, secundario: true, quebra: false, margem: new Thickness(0));
        nome.FontSize = 11;
        cabeca.Children.Add(nome);
        var bloco = new StackPanel();
        bloco.Children.Add(cabeca);
        foreach (var item in g.Itens) bloco.Children.Add(Item(item));
        return bloco;
    }

    private async Task Preencher(Image imagem, string appId)
    {
        try { imagem.Source = await _icone(appId); }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("PainelNotificacoes.Icone", e); }
    }

    private Border Item(ItemPainelNotificacao n)
    {
        var textos = new StackPanel();
        var titulo = new TextBlock { Text = n.Titulo ?? "", FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        textos.Children.Add(titulo);
        if (!string.IsNullOrWhiteSpace(n.Texto))
            textos.Children.Add(new TextBlock { Text = n.Texto, TextWrapping = TextWrapping.Wrap, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) });
        var quando = Texto(n.Quando, secundario: true, quebra: false, margem: new Thickness(0, 2, 0, 0));
        quando.FontSize = 11;
        textos.Children.Add(quando);

        var fechar = new TextBlock { Text = "×", FontSize = 14, Cursor = Cursors.Hand, Padding = new Thickness(6, 0, 2, 0), ToolTip = "Limpar" };
        fechar.SetResourceReference(TextBlock.ForegroundProperty, "TextoSecundario");
        fechar.MouseLeftButtonUp += (_, e) => { e.Handled = true; _vm.LimparCommand.Execute(n); };
        var linha = new DockPanel();
        DockPanel.SetDock(fechar, Dock.Right);
        linha.Children.Add(fechar);
        linha.Children.Add(textos);

        var item = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(16, 0, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = linha, ToolTip = "Abrir o app" };
        item.MouseEnter += (_, _) => item.SetResourceReference(Border.BackgroundProperty, "Superficie");
        item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
        item.MouseLeftButtonUp += (_, _) => _vm.AbrirAppCommand.Execute(n);
        return item;
    }

    private StackPanel Rodape(bool tudo)
    {
        var acoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 8, 0, 0) };
        if (tudo) acoes.Children.Add(Pilula.Criar("Limpar tudo", Pilula.Estilo.Contorno, () => _vm.LimparTudoCommand.Execute(null)));
        acoes.Children.Add(Pilula.Criar("Abrir a central do Windows", Pilula.Estilo.Contorno, () => _vm.AbrirCentralCommand.Execute(null)));
        return acoes;
    }

    private static TextBlock Texto(string texto, bool secundario, bool quebra, Thickness margem)
    {
        var t = new TextBlock { Text = texto, Margin = margem, TextWrapping = quebra ? TextWrapping.Wrap : TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        if (secundario) t.SetResourceReference(TextBlock.ForegroundProperty, "TextoSecundario");
        return t;
    }

    private static TextBlock Link(string texto, Action aoClicar)
    {
        var t = new TextBlock { Text = texto, FontSize = 11, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Ambar");
        t.MouseLeftButtonUp += (_, e) => { e.Handled = true; aoClicar(); };
        return t;
    }
}

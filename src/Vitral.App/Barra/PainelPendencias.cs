using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// A lista de pendências que desce do notch (spec da resposta §4.2, notch-resposta.html): uma linha por sessão; o clique
/// mostra a mensagem inteira com "Abrir no Claude" e "Descartar"; o "×" tira direto.
internal sealed class PainelPendencias : StackPanel
{
    private readonly Pendencias _pendencias;
    private Pendencias.Pendencia? _aberta;

    /// A lista ficou vazia: o painel pode fechar.
    public event Action? Vazia;

    public PainelPendencias(Pendencias pendencias)
    {
        _pendencias = pendencias;
        _pendencias.Mudou += Montar;
        Montar();
    }

    private void Montar()
    {
        Children.Clear();
        if (_pendencias.Quantas == 0)
        {
            var vazio = new TextBlock { Text = "Nenhuma sessão esperando", Margin = new Thickness(8) };
            vazio.SetResourceReference(TextBlock.ForegroundProperty, "TextoSecundario");
            Children.Add(vazio);
            Vazia?.Invoke();
            return;
        }
        if (_aberta is not null && !_pendencias.Itens.Contains(_aberta)) _aberta = null;
        foreach (var p in _pendencias.Itens) Children.Add(Item(p));
    }

    private Border Item(Pendencias.Pendencia p)
    {
        bool aberto = p == _aberta;

        // Cores da aparência das ilhas (o painel desce delas), pelas chaves do tema.
        var cabeca = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        cabeca.Inlines.Add(new Run(p.Sessao) { FontWeight = FontWeights.SemiBold });
        var quando = new Run($"   {(p.Tipo == TipoAviso.Esperando ? "esperando você" : "terminou")} · {p.Hora:HH:mm}") { FontSize = 11 };
        quando.SetResourceReference(TextElement.ForegroundProperty, "TextoSecundario");
        cabeca.Inlines.Add(quando);
        var textos = new StackPanel();
        textos.Children.Add(cabeca);
        if (aberto)
        {
            var inteiro = new TextBlock { Text = p.Completo, TextWrapping = TextWrapping.Wrap, LineHeight = 17 };
            textos.Children.Add(new ScrollViewer
            {
                MaxHeight = 110, Margin = new Thickness(0, 4, 0, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false,
                Content = inteiro,
            });
            var acoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-3, 8, 0, 0) };
            acoes.Children.Add(Pilula.Criar("Abrir no Claude", Pilula.Estilo.Principal, () =>
            {
                Claude.AbrirClaude.Trazer(p.AppId); // a conversa que originou a pendência
                _pendencias.Saiu(p);
            }));
            acoes.Children.Add(Pilula.Criar("Descartar", Pilula.Estilo.Contorno, () => _pendencias.Saiu(p)));
            textos.Children.Add(acoes);
        }
        else
        {
            var primeira = p.Completo.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            textos.Children.Add(new TextBlock { Text = primeira, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) });
        }

        var fechar = new TextBlock { Text = "×", FontSize = 14, Cursor = Cursors.Hand, Padding = new Thickness(6, 0, 2, 0), ToolTip = "Descartar" };
        fechar.SetResourceReference(TextBlock.ForegroundProperty, "TextoSecundario");
        fechar.MouseLeftButtonUp += (_, e) => { e.Handled = true; _pendencias.Saiu(p); };
        var icone = JanelaNotch.IconeClaude(18);
        icone.Margin = new Thickness(0, 1, 10, 0);
        icone.VerticalAlignment = VerticalAlignment.Top;
        var linha = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icone, Dock.Left);
        DockPanel.SetDock(fechar, Dock.Right);
        linha.Children.Add(icone);
        linha.Children.Add(fechar);
        linha.Children.Add(textos);

        var item = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = linha };
        if (aberto) item.SetResourceReference(Border.BackgroundProperty, "Superficie");
        item.MouseEnter += (_, _) => item.SetResourceReference(Border.BackgroundProperty, "Superficie");
        item.MouseLeave += (_, _) => { if (p != _aberta) item.Background = Brushes.Transparent; };
        item.MouseLeftButtonUp += (_, _) =>
        {
            _aberta = aberto ? null : p;
            Montar();
        };
        return item;
    }
}

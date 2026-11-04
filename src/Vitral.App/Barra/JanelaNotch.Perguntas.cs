using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vitral.App.Tema;
using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// A ficha de pergunta (spec das perguntas §3): a pergunta do Claude com as opções em botões, "Enviar" na múltipla,
/// "1/2" com várias e "Responder na conversa" sempre. O estado decide; aqui só o desenho e os cliques.
internal sealed partial class JanelaNotch
{
    private const double LarguraPergunta = 460;

    private readonly StackPanel _painelPergunta = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18, 12, 18, 10), Opacity = 0, Visibility = Visibility.Collapsed };
    private readonly TextBlock _perguntaSessao = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _perguntaContador = new() { FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _perguntaTexto = new()
    {
        FontSize = 12, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 34, Margin = new Thickness(0, 6, 0, 8),
    };
    private readonly WrapPanel _perguntaOpcoes = new();
    private readonly StackPanel _perguntaRodape = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _perguntaConfirmacao = new() { FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly ScaleTransform _tempoPergunta = new(1, 1);

    /// O que a ficha está desenhando (pedido e pergunta), para não refazer os botões a cada tique.
    private (string Id, int Pergunta, string Marcadas, int NaFila)? _perguntaMostrada;

    /// A última ficha da fila era uma pergunta (durante a confirmação, o pedido já saiu).
    private bool _fichaDePergunta;

    private void MontarPergunta()
    {
        var icone = IconeClaude(20);
        icone.Margin = new Thickness(0, 0, 8, 0);
        var titulo = new TextBlock { Text = "Claude pergunta", FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _perguntaSessao.SetResourceReference(TextBlock.ForegroundProperty, "TextoSecundario");
        _perguntaContador.SetResourceReference(TextBlock.ForegroundProperty, "TextoSecundario");
        var topo = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(_perguntaContador, Dock.Right);
        topo.Children.Add(_perguntaContador);
        var esquerda = new StackPanel { Orientation = Orientation.Horizontal };
        esquerda.Children.Add(icone);
        esquerda.Children.Add(titulo);
        esquerda.Children.Add(_perguntaSessao);
        topo.Children.Add(esquerda);

        var trilho = new Border { Height = 2, Margin = new Thickness(0, 9, 0, 0), CornerRadius = new CornerRadius(1) };
        trilho.SetResourceReference(Border.BackgroundProperty, "Linha");
        var barra = new Border { CornerRadius = new CornerRadius(1), RenderTransform = _tempoPergunta, RenderTransformOrigin = new Point(0, 0.5) };
        barra.SetResourceReference(Border.BackgroundProperty, "Texto");
        trilho.Child = barra;

        _painelPergunta.Children.Add(topo);
        _painelPergunta.Children.Add(_perguntaTexto);
        _painelPergunta.Children.Add(_perguntaOpcoes);
        _painelPergunta.Children.Add(_perguntaRodape);
        _painelPergunta.Children.Add(_perguntaConfirmacao);
        _painelPergunta.Children.Add(trilho);
    }

    private static bool EhPergunta(PedidoPermissao? p) => p?.Ferramenta == RegrasPerguntas.Ferramenta;

    /// Textos, botões e barra da pergunta atual; refaz os botões só quando a pergunta ou as marcações mudam.
    private void AtualizarPergunta()
    {
        bool confirmando = _estado.Confirmando;
        _perguntaOpcoes.Visibility = _perguntaRodape.Visibility = _perguntaTexto.Visibility = confirmando ? Visibility.Collapsed : Visibility.Visible;
        _perguntaConfirmacao.Visibility = confirmando ? Visibility.Visible : Visibility.Collapsed;
        if (confirmando)
        {
            _perguntaConfirmacao.Text = _estado.UltimaEscolha == EscolhaPermissao.Conversa ? "Na conversa" : "✓ Respondido";
            _perguntaConfirmacao.SetResourceReference(TextBlock.ForegroundProperty, "Texto");
            return;
        }
        if (_estado.PedidoAtual is not { } p) return;
        var perguntas = p.Perguntas;
        bool cabe = RegrasPerguntas.Cabe(perguntas);
        int indice = cabe ? _estado.PerguntaAtual : 0;
        var marcadas = string.Join("|", _estado.Marcadas.Order(StringComparer.Ordinal));
        if (_perguntaMostrada == (p.Id, indice, marcadas, _estado.NaFila)) return;
        bool novoPedido = _perguntaMostrada?.Id != p.Id;
        _perguntaMostrada = (p.Id, indice, marcadas, _estado.NaFila);

        _perguntaSessao.Text = $"  ·  {p.Sessao}";
        // "1/2" com várias perguntas e "+N" com pedidos esperando atrás.
        var contador = cabe && perguntas!.Count > 1 ? $"{indice + 1}/{perguntas.Count}" : "";
        _perguntaContador.Text = _estado.NaFila > 0 ? (contador.Length > 0 ? $"{contador}  ·  +{_estado.NaFila}" : $"+{_estado.NaFila}") : contador;
        var pergunta = perguntas is { Count: > 0 } ? perguntas[indice] : null;
        _perguntaTexto.Text = pergunta?.Texto ?? p.Resumo;
        _perguntaTexto.ToolTip = new TextBlock { Text = pergunta?.Texto ?? p.Completo ?? p.Resumo, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 };

        _perguntaOpcoes.Children.Clear();
        _perguntaRodape.Children.Clear();
        if (cabe && pergunta is not null)
            foreach (var opcao in pergunta.Opcoes)
            {
                bool marcada = _estado.Marcadas.Contains(opcao.Rotulo);
                var rotulo = opcao.Rotulo;
                var botao = Pilula.Criar(pergunta.Multipla ? (marcada ? "✓ " : "") + rotulo : rotulo,
                    marcada || (!pergunta.Multipla && opcao == pergunta.Opcoes[0]) ? Pilula.Estilo.Principal : Pilula.Estilo.Contorno,
                    () => { if (pergunta.Multipla) _estado.Marcar(p.Id, rotulo); else _estado.Escolheu(p.Id, rotulo); });
                botao.Margin = new Thickness(0, 0, 6, 6);
                if (opcao.Descricao is { Length: > 0 } descricao) botao.ToolTip = descricao;
                _perguntaOpcoes.Children.Add(botao);
            }
        if (cabe && pergunta is { Multipla: true })
        {
            var enviar = Pilula.Criar("Enviar", Pilula.Estilo.Principal, () => _estado.Enviar(p.Id));
            enviar.IsEnabled = _estado.PodeEnviar;
            enviar.Opacity = _estado.PodeEnviar ? 1 : 0.45;
            enviar.Margin = new Thickness(0, 0, 6, 0);
            _perguntaRodape.Children.Add(enviar);
        }
        var conversa = Pilula.Criar("Responder na conversa", Pilula.Estilo.Contorno, () =>
        {
            var appId = p.AppId;
            if (_estado.Respondeu(p.Id, EscolhaPermissao.Conversa) is not null) Claude.AbrirClaude.Trazer(appId);
        });
        _perguntaRodape.Children.Add(conversa);

        if (!novoPedido) return;
        // A barra esvazia no tempo que falta dos 5 min.
        double tempo = _estado.TempoDoAtual;
        double falta = Math.Max(0, tempo - (DateTime.Now - (_estado.ChegadaDoAtual ?? DateTime.Now)).TotalSeconds);
        _tempoPergunta.BeginAnimation(ScaleTransform.ScaleXProperty, Movimento.Ligado
            ? new DoubleAnimation(falta / tempo, 0, new Duration(TimeSpan.FromSeconds(falta)))
            : new DoubleAnimation(1, new Duration(TimeSpan.Zero)));
    }
}

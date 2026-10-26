using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vitral.App.Tema;
using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// A ficha de resposta ao Claude (spec da resposta §4.2, forma B de notch-resposta.html): a mensagem em duas linhas com
/// "ver tudo", a caixa de resposta e a barra de 60 s, que para quando o Gustavo começa a digitar. O notch não pega foco;
/// ao clicar na caixa ele passa a pegar, e ao terminar a ficha devolve o foco à janela onde o Gustavo estava.
internal sealed partial class JanelaNotch
{
    private const double MensagemFechada = 34, MensagemAberta = 150;

    [LibraryImport("user32.dll")] private static partial IntPtr GetForegroundWindow();
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetForegroundWindow(IntPtr h);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWindow(IntPtr h);

    private static readonly Brush CorBarra = Congelado(new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)));
    private static readonly Brush CorBarraParada = Congelado(new SolidColorBrush(Color.FromRgb(0x9C, 0xA0, 0xAA)));

    private readonly StackPanel _painelResposta = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18, 12, 18, 10), Opacity = 0, Visibility = Visibility.Collapsed };
    private readonly TextBlock _respostaSessao = new() { FontSize = 11, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _respostaTexto = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 17, Margin = new Thickness(0, 0, 12, 0) }; // respiro para a barra de rolagem
    private readonly ScrollViewer _respostaRolagem = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = MensagemFechada, Margin = new Thickness(0, 8, 0, 0), Focusable = false };
    private readonly TextBlock _verTudo = new() { Text = "ver tudo ⌄", FontSize = 11, Cursor = Cursors.Hand, Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBox _caixa = new()
    {
        Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 12, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 8000,
        MaxHeight = 60, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0),
    };
    private readonly TextBlock _dicaCaixa = new() { Text = "Responder ao Claude… (Enter envia, Esc cancela)", FontSize = 12, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _linhaCaixa = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _respostaConfirmacao = new() { Text = "✓ Enviado", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Height = 28, Visibility = Visibility.Collapsed };
    private readonly ScaleTransform _tempoResposta = new(1, 1);
    private readonly Border _barraResposta = new() { CornerRadius = new CornerRadius(1) };
    private readonly Dictionary<string, TaskCompletionSource<string?>> _esperandoResposta = [];
    private Border _enviar = null!;
    private string? _respostaMostrada;
    private bool _mensagemAberta, _ativada, _devolverFoco;
    private IntPtr _janelaAnterior;

    /// As sessões que ficaram sem resposta (a lista que desce do notch).
    public Pendencias Pendencias => _estado.Pendencias;

    /// Uma sessão terminou fora do app: entra na ficha (ou na fila) e devolve o texto digitado, ou null sem resposta.
    public Task<string?> Responder(PedidoResposta pedido)
    {
        var espera = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _esperandoResposta[pedido.Id] = espera;
        _estado.PediuResposta(pedido);
        return espera.Task;
    }

    /// Sem notch visível: a sessão vai direto para as pendências.
    public void SemNotch(PedidoResposta pedido) => _estado.SemNotch(pedido);

    private void MontarResposta()
    {
        var secundario = (Brush)Application.Current.Resources["TextoSecundario"];
        _respostaSessao.Foreground = secundario;
        _verTudo.Foreground = secundario;
        _dicaCaixa.Foreground = secundario;
        _caixa.Foreground = (Brush)Application.Current.Resources["Texto"];
        _caixa.CaretBrush = Brushes.White;

        var icone = IconeClaude(22);
        icone.Margin = new Thickness(0, 0, 10, 0);
        var abrir = new TextBlock { Text = "abrir no Claude ›", FontSize = 11, Foreground = secundario, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        abrir.MouseLeftButtonUp += (_, e) => { e.Handled = true; AbrirNoClaude(); };
        var topo = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icone, Dock.Left);
        DockPanel.SetDock(abrir, Dock.Right);
        var titulo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titulo.Children.Add(new TextBlock { Text = "Claude terminou", FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        titulo.Children.Add(_respostaSessao);
        topo.Children.Add(icone);
        topo.Children.Add(abrir);
        topo.Children.Add(titulo);

        // A mensagem: duas linhas esmaecidas; "ver tudo" (ou a roda do mouse) abre até ~10 linhas roláveis.
        _respostaTexto.Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xD8, 0xDE));
        _respostaRolagem.Content = _respostaTexto;
        _respostaRolagem.PreviewMouseWheel += (_, e) => { if (!_mensagemAberta) { AbrirMensagem(); e.Handled = true; } };
        _verTudo.MouseLeftButtonUp += (_, e) => { e.Handled = true; AbrirMensagem(); };

        var campo = new Grid();
        campo.Children.Add(_dicaCaixa);
        campo.Children.Add(_caixa);
        var moldura = new Border
        {
            CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(12, 4, 12, 4), MinHeight = 28,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF)), Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
            Child = campo, Cursor = Cursors.IBeam,
        };
        moldura.PreviewMouseDown += (_, _) => Ativar();
        _enviar = Pilula.Criar("Enviar", Pilula.Estilo.Principal, Enviar);
        _enviar.VerticalAlignment = VerticalAlignment.Bottom;
        _linhaCaixa.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _linhaCaixa.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_enviar, 1);
        _linhaCaixa.Children.Add(moldura);
        _linhaCaixa.Children.Add(_enviar);

        _caixa.TextChanged += (_, _) =>
        {
            _dicaCaixa.Visibility = _caixa.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            LigarEnviar();
            if (_respostaMostrada is { } id) _estado.Digitou(id, _caixa.Text);
        };
        _caixa.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; Enviar(); }
            else if (e.Key == Key.Escape && _respostaMostrada is { } id)
            {
                e.Handled = true;
                _devolverFoco = true; // o Gustavo encerrou: o foco volta para onde ele estava
                _estado.Cancelou(id);
            }
        };
        // Shift+Enter faz a caixa crescer: o corpo do notch acompanha.
        _caixa.SizeChanged += (_, _) => { if (_estado.Modo == ModoNotch.Resposta) Ajustar(animar: false); };

        var trilho = new Border { Height = 2, Margin = new Thickness(0, 9, 0, 0), CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)) };
        _barraResposta.Background = CorBarra;
        _barraResposta.RenderTransform = _tempoResposta;
        _barraResposta.RenderTransformOrigin = new Point(0, 0.5);
        trilho.Child = _barraResposta;

        _painelResposta.Children.Add(topo);
        _painelResposta.Children.Add(_respostaRolagem);
        _painelResposta.Children.Add(_verTudo);
        _painelResposta.Children.Add(_linhaCaixa);
        _painelResposta.Children.Add(_respostaConfirmacao);
        _painelResposta.Children.Add(trilho);

        _estado.RespostaEncerrada += (pedido, texto) =>
        {
            if (_esperandoResposta.Remove(pedido.Id, out var espera)) espera.TrySetResult(texto);
        };
    }

    private void LigarEnviar()
    {
        bool tem = !string.IsNullOrWhiteSpace(_caixa.Text);
        _enviar.IsEnabled = tem;
        _enviar.Opacity = tem ? 1 : 0.4;
    }

    private void Enviar()
    {
        if (_respostaMostrada is not { } id) return;
        _devolverFoco = true;
        if (_estado.Enviou(id)) Desativar();
        else _devolverFoco = false;
    }

    /// "abrir no Claude ›": solta a sessão (sem ir para as pendências) e traz o app, sem devolver o foco antes (a janela
    /// anterior piscaria).
    private void AbrirNoClaude()
    {
        _janelaAnterior = IntPtr.Zero;
        if (_respostaMostrada is { } id) _estado.DesistiuResposta(id);
        Desativar();
        Claude.AbrirClaude.Trazer();
    }

    private void AbrirMensagem()
    {
        _mensagemAberta = true;
        _respostaRolagem.MaxHeight = MensagemAberta;
        _respostaRolagem.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _respostaRolagem.OpacityMask = null;
        _verTudo.Visibility = Visibility.Collapsed;
        Ajustar(animar: true);
    }

    private void FecharMensagem()
    {
        _mensagemAberta = false;
        _respostaRolagem.MaxHeight = MensagemFechada;
        _respostaRolagem.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _respostaRolagem.ScrollToTop();
        var esmaecer = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        esmaecer.GradientStops.Add(new GradientStop(Colors.Black, 0.55));
        esmaecer.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        _respostaRolagem.OpacityMask = esmaecer;
        _respostaTexto.Measure(new Size(NotchMedidas.LarguraResposta - 36, double.PositiveInfinity));
        bool sobra = _respostaTexto.DesiredSize.Height > MensagemFechada + 1;
        _verTudo.Visibility = sobra ? Visibility.Visible : Visibility.Collapsed;
        if (!sobra) _respostaRolagem.OpacityMask = null;
    }

    /// O clique na caixa: o notch passa a pegar foco para receber o teclado.
    private void Ativar()
    {
        if (!_ativada)
        {
            var h = new WindowInteropHelper(this).Handle;
            var frente = GetForegroundWindow();
            _janelaAnterior = frente == h ? IntPtr.Zero : frente;
            SetLong(h, GwlExStyle, (IntPtr)((long)GetLong(h, GwlExStyle) & ~WsExNoActivate));
            _ativada = true;
            Activate();
        }
        _caixa.Focus();
        Keyboard.Focus(_caixa);
        if (_respostaMostrada is { } id) _estado.Focou(id); // o tempo para já no clique
    }

    /// Fim da ficha: o notch volta a não pegar foco. O foco volta à janela onde o Gustavo estava só quando ele mesmo
    /// encerrou (Enviar, Esc) e o notch ainda está na frente. Se a ficha saiu por outro motivo no meio da digitação
    /// (pedido de permissão, tempo, a sessão largada), o notch continua na frente sem nada com foco: o resto das teclas se
    /// perde, em vez de cair num terminal ou num chat.
    private void Desativar()
    {
        if (!_ativada) return;
        _ativada = false;
        var h = new WindowInteropHelper(this).Handle;
        SetLong(h, GwlExStyle, (IntPtr)((long)GetLong(h, GwlExStyle) | WsExNoActivate));
        Keyboard.ClearFocus();
        if (_devolverFoco && GetForegroundWindow() == h && _janelaAnterior != IntPtr.Zero && IsWindow(_janelaAnterior))
            SetForegroundWindow(_janelaAnterior);
        _devolverFoco = false;
        _janelaAnterior = IntPtr.Zero;
    }

    /// Textos, caixa e barra da ficha atual.
    private void AtualizarResposta()
    {
        bool confirmando = _estado.ConfirmandoResposta;
        _linhaCaixa.Visibility = confirmando ? Visibility.Collapsed : Visibility.Visible;
        _respostaConfirmacao.Visibility = confirmando ? Visibility.Visible : Visibility.Collapsed;
        if (confirmando) { Desativar(); return; }
        if (_estado.RespostaAtual is not { } p) return;
        if (_respostaMostrada != p.Id)
        {
            Desativar();
            _respostaMostrada = p.Id;
            _respostaTexto.Text = p.Completo;
            _caixa.Text = _estado.Rascunho; // volta com o texto, se um pedido de permissão passou à frente
            LigarEnviar();
            FecharMensagem();
            _barraResposta.Background = CorBarra;
            double falta = Math.Max(0, EstadoNotch.TempoResposta - (DateTime.Now - (_estado.ChegadaDaResposta ?? DateTime.Now)).TotalSeconds);
            _tempoResposta.BeginAnimation(ScaleTransform.ScaleXProperty, Movimento.Ligado && !_estado.Digitando
                ? new DoubleAnimation(falta / EstadoNotch.TempoResposta, 0, new Duration(TimeSpan.FromSeconds(falta)))
                : new DoubleAnimation(1, new Duration(TimeSpan.Zero)));
        }
        var fila = _estado.NaFilaResposta > 0 ? $"  +{_estado.NaFilaResposta}" : "";
        _respostaSessao.Text = $"{p.Sessao} · {p.Hora:HH:mm}{fila}";
        if (_estado.Digitando && _barraResposta.Background != CorBarraParada)
        {
            // Começou a digitar: a barra para onde estava e fica cinza.
            var agora = _tempoResposta.ScaleX;
            _tempoResposta.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _tempoResposta.ScaleX = agora;
            _barraResposta.Background = CorBarraParada;
        }
    }

    /// Ao fechar, as fichas que esperavam saem sem resposta (a sessão termina normal).
    private void SoltarRespostas()
    {
        foreach (var espera in _esperandoResposta.Values) espera.TrySetResult(null);
        _esperandoResposta.Clear();
    }
}

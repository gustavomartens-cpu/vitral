using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Vitral.App.Tema;
using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// O notch vivo (spec do notch vivo §3): janela transparente com o corpo preto sólido e as orelhas, do tamanho máximo
/// do corpo; só o desenho muda de tamanho dentro dela. Mostra o relógio (normal e lembrete), o aviso do Claude, ou as
/// laterais ao passar o mouse (Claude à esquerda, notificações à direita).
internal sealed partial class JanelaNotch : Window, IPedePainel
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
    private const double Folga = 36; // o mesmo respiro de 18 px de cada lado da ilha do relógio de antes

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static partial IntPtr GetLong(IntPtr h, int i);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static partial IntPtr SetLong(IntPtr h, int i, IntPtr v);

    private static readonly Brush Corpo = Congelado(new SolidColorBrush(Color.FromRgb(0x0B, 0x0C, 0x10)));
    private static readonly Brush CorClaude = Congelado(new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x57)));

    public static readonly DependencyProperty LarguraCorpoProperty =
        DependencyProperty.Register(nameof(LarguraCorpo), typeof(double), typeof(JanelaNotch), new PropertyMetadata(120d, Redesenhar));
    public static readonly DependencyProperty AlturaCorpoProperty =
        DependencyProperty.Register(nameof(AlturaCorpo), typeof(double), typeof(JanelaNotch), new PropertyMetadata(NotchMedidas.AlturaNormal, Redesenhar));
    public static readonly DependencyProperty RaioCorpoProperty =
        DependencyProperty.Register(nameof(RaioCorpo), typeof(double), typeof(JanelaNotch), new PropertyMetadata(NotchMedidas.RaioNormal, Redesenhar));

    public double LarguraCorpo { get => (double)GetValue(LarguraCorpoProperty); set => SetValue(LarguraCorpoProperty, value); }
    public double AlturaCorpo { get => (double)GetValue(AlturaCorpoProperty); set => SetValue(AlturaCorpoProperty, value); }
    public double RaioCorpo { get => (double)GetValue(RaioCorpoProperty); set => SetValue(RaioCorpoProperty, value); }

    private readonly Border _topo = new() { Background = Corpo, Height = PosicaoIlhas.NotchSobra + 1, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Path _forma = new() { Fill = Corpo, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    // Sobre o corpo opaco, o fundo transparente recebe o clique em qualquer ponto; fora do corpo, o clique atravessa.
    private readonly Grid _conteudo = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, ClipToBounds = true };
    private readonly FrameworkElement _relogio;
    private readonly Border _meio = new() { Background = Brushes.Transparent, Cursor = Cursors.Hand };
    private readonly StackPanel _linhaRelogio = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _ponto;
    private readonly Grid _painelAviso = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 18, 0), Opacity = 0, Visibility = Visibility.Collapsed };

    // Ficha do pedido de permissão (spec das permissões §4.1)
    private readonly StackPanel _painelPedido = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 18, 0), Opacity = 0, Visibility = Visibility.Collapsed };
    private readonly TextBlock _pedidoTitulo = new() { FontWeight = FontWeights.SemiBold, FontSize = 12 };
    private readonly TextBlock _pedidoSessao = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _pedidoResumo = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _pedidoFila = new() { FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    private readonly StackPanel _botoes = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _confirmacao = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Height = 28, Visibility = Visibility.Collapsed };
    private readonly ScaleTransform _tempoRestante = new(1, 1);
    private readonly Dictionary<string, TaskCompletionSource<EscolhaPermissao?>> _esperando = [];
    private string? _pedidoMostrado;
    private readonly TextBlock _titulo = new() { FontWeight = FontWeights.SemiBold, FontSize = 12 };
    private readonly TextBlock _detalhe = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _abrir = new() { FontSize = 11, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _simbolo = new() { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Image _logoVitral = new() { Width = 22, Height = 22, Visibility = Visibility.Collapsed };
    private readonly Border _esquerda, _direita;
    private readonly TextBlock _esquerdaTitulo = new() { FontSize = 11, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _esquerdaSessao = new() { FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly EstadoNotch _estado = new(() => DateTime.Now);
    private readonly DispatcherTimer _tique = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public event Action<Painel>? Pediu;

    /// Clique no aviso de atualização: reiniciar para a versão nova (spec da atualização §5.3).
    public event Action? PediuReiniciar;

    /// A largura do corpo no estado normal mudou (o relógio cresceu ou encolheu): as outras ilhas se reposicionam.
    public event Action? LarguraNormalMudou;

    public JanelaNotch(FrameworkElement relogio)
    {
        Title = "Vitral · Relógio";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        Width = NotchMedidas.JanelaLargura;
        Height = NotchMedidas.JanelaAltura;
        Foreground = (Brush)Application.Current.Resources["Texto"];
        FontFamily = (FontFamily)Application.Current.Resources["FonteCorpo"];
        FontSize = 12;

        _relogio = relogio;
        _relogio.IsHitTestVisible = false; // o clique é do notch (decide entre calendário e Claude)
        _ponto = new Ellipse
        {
            Width = 6, Height = 6, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
            Fill = (Brush)Application.Current.Resources["Ambar"],
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = (Color)Application.Current.Resources["CorAmbar"], BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 },
        };
        _linhaRelogio.Children.Add(_relogio);
        _linhaRelogio.Children.Add(_ponto);
        MontarAviso();
        _esquerda = MontarEsquerda();
        _direita = MontarDireita();

        var miolo = new Grid();
        miolo.Children.Add(_linhaRelogio);
        miolo.Children.Add(_painelAviso);
        MontarPedido();
        miolo.Children.Add(_painelPedido);
        _meio.Child = miolo;
        _conteudo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _conteudo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _conteudo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_esquerda, 0);
        Grid.SetColumn(_meio, 1);
        Grid.SetColumn(_direita, 2);
        _conteudo.Children.Add(_esquerda);
        _conteudo.Children.Add(_meio);
        _conteudo.Children.Add(_direita);

        // A forma ocupa a largura total (corpo + orelhas) a partir da borda da tela; o pedaço acima da borda (fora da
        // tela) também é pintado, para a costura com a borda não aparecer.
        _forma.Margin = new Thickness(0, PosicaoIlhas.NotchSobra, 0, 0);
        var raiz = new Grid(); // sem fundo: a parte transparente deixa o clique passar
        raiz.Children.Add(_topo);
        raiz.Children.Add(_forma);
        raiz.Children.Add(_conteudo);
        Content = raiz;

        _estado.Mudou += AoMudarEstado;
        _estado.Encerrado += (pedido, escolha) =>
        {
            if (_esperando.Remove(pedido.Id, out var espera)) espera.TrySetResult(escolha);
        };
        _tique.Tick += (_, _) => _estado.Tique();
        _conteudo.MouseEnter += (_, _) => { _estado.MouseEntrou(); _tique.Start(); };
        _conteudo.MouseLeave += (_, _) => { _estado.MouseSaiu(); if (_estado.Modo is not (ModoNotch.Aviso or ModoNotch.Permissao)) _tique.Stop(); };
        _meio.MouseLeftButtonUp += (_, _) => Clicou(ParteNotch.Meio);
        _esquerda.MouseLeftButtonUp += (_, _) => Clicou(ParteNotch.Esquerda);
        _direita.MouseLeftButtonUp += (_, _) => Clicou(ParteNotch.Direita);
        _relogio.SizeChanged += (_, _) => { if (_estado.Modo != ModoNotch.Aviso) Ajustar(animar: false); LarguraNormalMudou?.Invoke(); };
        Loaded += (_, _) => Ajustar(animar: false);
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetLong(h, GwlExStyle, (IntPtr)((long)GetLong(h, GwlExStyle) | WsExToolWindow | WsExNoActivate));
        };
        Closed += (_, _) => { _tique.Stop(); SoltarPedidos(); };
    }

    /// Símbolo do Claude (a partícula com raios abertos), desenhado em vetor no laranja dele. Marca da Anthropic: uso
    /// pessoal; se o Vitral for distribuído, trocar por um símbolo próprio (ideias-pendentes).
    private static readonly DrawingImage SimboloClaude = CriarSimbolo();

    private static DrawingImage CriarSimbolo()
    {
        const string raios = "M0,-12 L0,-44 M0,12 L0,40 M-12,0 L-42,0 M12,0 L44,0 M8,-8 L30,-32 M-8,8 L-28,30 "
                           + "M-8,-8 L-31,-29 M8,8 L32,27 M11,-4 L40,-17 M-11,4 L-39,15 M-4,-11 L-16,-41 M4,11 L15,38";
        var caneta = new Pen(CorClaude, 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var grupo = new DrawingGroup();
        grupo.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(-50, -50, 100, 100))));
        grupo.Children.Add(new GeometryDrawing(null, caneta, Geometry.Parse(raios)));
        var imagem = new DrawingImage(grupo);
        imagem.Freeze();
        return imagem;
    }

    private static Image IconeClaude(double tamanho) => new()
    {
        Width = tamanho, Height = tamanho, Source = SimboloClaude, VerticalAlignment = VerticalAlignment.Center,
    };

    private void MontarAviso()
    {
        // O símbolo do Claude, ou o logo do Vitral no aviso de atualização.
        _simbolo.Children.Add(IconeClaude(22));
        _logoVitral.Source = (ImageSource)Application.Current.Resources["Logo"];
        _simbolo.Children.Add(_logoVitral);
        _detalhe.Foreground = (Brush)Application.Current.Resources["TextoSecundario"];
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(_titulo);
        textos.Children.Add(_detalhe);
        _abrir.Foreground = (Brush)Application.Current.Resources["Ambar"];
        var linha = new DockPanel();
        DockPanel.SetDock(_simbolo, Dock.Left);
        DockPanel.SetDock(_abrir, Dock.Right);
        linha.Children.Add(_simbolo);
        linha.Children.Add(_abrir);
        linha.Children.Add(textos);
        _painelAviso.Children.Add(linha);
    }

    /// Lateral esquerda (spec §3.4): o Claude, com o aviso pendente se houver.
    private Border MontarEsquerda()
    {
        var icone = IconeClaude(18);
        icone.Margin = new Thickness(0, 0, 8, 0);
        _esquerdaSessao.Foreground = (Brush)Application.Current.Resources["TextoSecundario"];
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(_esquerdaTitulo);
        textos.Children.Add(_esquerdaSessao);
        var linha = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 6, 0) };
        linha.Children.Add(icone);
        linha.Children.Add(textos);
        return Lateral(linha, "Abrir o Claude");
    }

    /// Lateral direita (spec §3.4): atalho para a central de notificações do Windows.
    private Border MontarDireita()
    {
        var linha = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(6, 0, 18, 0) };
        linha.Children.Add(new TextBlock { Text = "", FontFamily = (FontFamily)Application.Current.Resources["FonteIcones"], FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        linha.Children.Add(new TextBlock { Text = "Notificações", FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        return Lateral(linha, "Central de notificações");
    }

    private static Border Lateral(UIElement conteudo, string dica) => new()
    {
        Width = NotchMedidas.LarguraLateral, Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = dica,
        Child = conteudo, Opacity = 0, Visibility = Visibility.Collapsed,
    };

    /// Um aviso chegou pelo canal.
    public void Avisar(AvisoClaude aviso)
    {
        bool atualizacao = aviso.Tipo == TipoAviso.Atualizacao;
        _simbolo.Children[0].Visibility = atualizacao ? Visibility.Collapsed : Visibility.Visible;
        _logoVitral.Visibility = atualizacao ? Visibility.Visible : Visibility.Collapsed;
        _abrir.Text = atualizacao ? "reiniciar ›" : "abrir ›";
        _titulo.Text = aviso.Tipo switch
        {
            TipoAviso.Terminou => "Claude terminou a tarefa",
            TipoAviso.Atualizacao => $"Vitral {aviso.Texto}",
            _ => "Claude está esperando você",
        };
        _detalhe.Text = atualizacao ? "clique para reiniciar e atualizar"
            : string.IsNullOrEmpty(aviso.Texto) ? aviso.Sessao : $"{aviso.Sessao} · {aviso.Texto}";
        _estado.Chegou(aviso);
    }

    /// O retângulo visível do corpo no estado normal, em DIPs de tela (o calendário se apoia nele).
    public Retangulo AreaDoRelogio =>
        new(Left + (Width - LarguraNormal) / 2, Top, LarguraNormal, PosicaoIlhas.NotchSobra + NotchMedidas.AlturaNormal);

    /// A largura do corpo com o relógio (o que a ilha do relógio ocupava antes).
    public double LarguraNormal
    {
        get
        {
            _relogio.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return Math.Ceiling(_relogio.DesiredSize.Width) + Folga;
        }
    }

    /// Centra a janela fixa no lugar do relógio calculado pelas ilhas.
    public void Colocar(Retangulo relogio)
    {
        Left = relogio.X + relogio.Largura / 2 - Width / 2;
        Top = relogio.Y;
    }

    private void Clicou(ParteNotch parte)
    {
        switch (_estado.Clicou(parte))
        {
            case AcaoNotch.AbrirClaude: Claude.AbrirClaude.Trazer(); break;
            case AcaoNotch.AbrirCalendario: Pediu?.Invoke(Painel.Calendario); break;
            case AcaoNotch.AbrirNotificacoes: Atalhos.CentralDeNotificacoes(); break;
            case AcaoNotch.Reiniciar: PediuReiniciar?.Invoke(); break;
        }
    }

    private void AoMudarEstado()
    {
        if (_estado.Modo is ModoNotch.Aviso or ModoNotch.Permissao || IsMouseOver) _tique.Start(); else _tique.Stop();
        Ajustar(animar: true);
    }

    /// Pedido de permissão chegou pelo canal: entra na ficha (ou na fila) e devolve a escolha, ou null sem resposta.
    public Task<EscolhaPermissao?> Pedir(PedidoPermissao pedido)
    {
        var espera = new TaskCompletionSource<EscolhaPermissao?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _esperando[pedido.Id] = espera;
        _estado.Pediu(pedido);
        return espera.Task;
    }

    /// Ao fechar, os pedidos que esperavam saem sem resposta (o Claude segue o pedido normal dele).
    private void SoltarPedidos()
    {
        foreach (var espera in _esperando.Values) espera.TrySetResult(null);
        _esperando.Clear();
    }

    private void MontarPedido()
    {
        var icone = IconeClaude(22);
        icone.Margin = new Thickness(0, 0, 10, 0);
        _pedidoSessao.Foreground = (Brush)Application.Current.Resources["TextoSecundario"];
        _pedidoFila.Foreground = (Brush)Application.Current.Resources["TextoSecundario"];
        _pedidoResumo.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        _pedidoResumo.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x8A));
        var codigo = new Border { Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1, 6, 1), Child = _pedidoResumo };
        var linhaResumo = new DockPanel { Margin = new Thickness(0, 2, 0, 0), LastChildFill = true };
        DockPanel.SetDock(_pedidoSessao, Dock.Left);
        linhaResumo.Children.Add(_pedidoSessao);
        linhaResumo.Children.Add(codigo);
        var textos = new StackPanel();
        textos.Children.Add(_pedidoTitulo);
        textos.Children.Add(linhaResumo);
        var linha = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icone, Dock.Left);
        DockPanel.SetDock(_pedidoFila, Dock.Right);
        linha.Children.Add(icone);
        linha.Children.Add(_pedidoFila);
        linha.Children.Add(textos);

        _botoes.Children.Add(BotaoPedido("Permitir", EscolhaPermissao.Permitir, principal: true));
        _botoes.Children.Add(BotaoPedido("Sempre nesta sessão", EscolhaPermissao.SempreNaSessao));
        _botoes.Children.Add(BotaoPedido("Negar", EscolhaPermissao.Negar, perigo: true));

        var trilho = new Border { Height = 2, Margin = new Thickness(0, 9, 0, 0), CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)) };
        trilho.Child = new Border { Background = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)), CornerRadius = new CornerRadius(1), RenderTransform = _tempoRestante, RenderTransformOrigin = new Point(0, 0.5) };

        _painelPedido.Children.Add(linha);
        _painelPedido.Children.Add(_botoes);
        _painelPedido.Children.Add(_confirmacao);
        _painelPedido.Children.Add(trilho);
    }

    /// Botão em pílula da ficha, nas cores escolhidas pelo Gustavo em 26/10 (branco e vermelho): Permitir branco sólido,
    /// Sempre com contorno claro, Negar com texto vermelho; afunda no clique.
    private Border BotaoPedido(string texto, EscolhaPermissao escolha, bool principal = false, bool perigo = false)
    {
        static SolidColorBrush Cor(byte a, byte r, byte g, byte b) { var p = new SolidColorBrush(Color.FromArgb(a, r, g, b)); p.Freeze(); return p; }
        var (fundo, hover, borda, corTexto) = (principal, perigo) switch
        {
            (true, _) => (Cor(0xFF, 0xF2, 0xF2, 0xF2), Cor(0xFF, 0xFF, 0xFF, 0xFF), Brushes.Transparent, Cor(0xFF, 0x11, 0x11, 0x11)),
            (_, true) => ((Brush)Brushes.Transparent, Cor(0x29, 0xE8, 0x54, 0x54), Brushes.Transparent, Cor(0xFF, 0xFF, 0x8A, 0x8A)),
            _ => (Brushes.Transparent, Cor(0x14, 0xFF, 0xFF, 0xFF), Cor(0x47, 0xFF, 0xFF, 0xFF), Cor(0xFF, 0xE6, 0xE6, 0xE6)),
        };
        var rotulo = new TextBlock
        {
            Text = texto, FontSize = 11.5, Foreground = corTexto, FontWeight = principal ? FontWeights.SemiBold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var botao = new Border
        {
            Height = 28, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(3, 0, 3, 0),
            Cursor = Cursors.Hand, Child = rotulo, Background = fundo, BorderBrush = borda, BorderThickness = new Thickness(1),
        };
        botao.MouseEnter += (_, _) => botao.Background = hover;
        botao.MouseLeave += (_, _) => botao.Background = fundo;
        Efeitos.SetAfundar(botao, true);
        botao.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true; // o clique é do botão, não do notch (que abriria o calendário)
            _estado.Respondeu(escolha);
        };
        return botao;
    }

    /// Textos e barra do pedido atual; a barra esvazia no tempo que falta dos 60 s.
    private void AtualizarPedido()
    {
        bool confirmando = _estado.Confirmando;
        _botoes.Visibility = confirmando ? Visibility.Collapsed : Visibility.Visible;
        _confirmacao.Visibility = confirmando ? Visibility.Visible : Visibility.Collapsed;
        if (confirmando)
        {
            _confirmacao.Text = _estado.UltimaEscolha switch
            {
                EscolhaPermissao.Permitir => "✓ Permitido",
                EscolhaPermissao.SempreNaSessao => "✓ Sempre nesta sessão",
                _ => "Negado",
            };
            _confirmacao.Foreground = _estado.UltimaEscolha == EscolhaPermissao.Negar
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x8A)) : (Brush)Application.Current.Resources["Texto"];
        }
        _pedidoFila.Text = _estado.NaFila > 0 ? $"+{_estado.NaFila}" : "";
        if (_estado.PedidoAtual is not { } p || _pedidoMostrado == p.Id) return;
        _pedidoMostrado = p.Id;
        _pedidoTitulo.Text = p.Titulo;
        _pedidoSessao.Text = $"{p.Sessao} · ";
        _pedidoResumo.Text = p.Resumo;
        _pedidoResumo.ToolTip = p.Resumo;
        double falta = Math.Max(0, EstadoNotch.TempoPedido - (DateTime.Now - (_estado.ChegadaDoAtual ?? DateTime.Now)).TotalSeconds);
        double inicio = falta / EstadoNotch.TempoPedido;
        _tempoRestante.BeginAnimation(ScaleTransform.ScaleXProperty, Movimento.Ligado
            ? new DoubleAnimation(inicio, 0, new Duration(TimeSpan.FromSeconds(falta)))
            : new DoubleAnimation(1, new Duration(TimeSpan.Zero)));
    }

    /// Leva o corpo e o conteúdo ao estado atual.
    private void Ajustar(bool animar)
    {
        var modo = _estado.Modo;
        bool laterais = _estado.Laterais && modo is ModoNotch.Normal or ModoNotch.Lembrete;
        double largura, altura, raio;
        if (modo == ModoNotch.Permissao)
        {
            AtualizarPedido();
            _painelPedido.Visibility = Visibility.Visible;
            _painelPedido.Measure(new Size(NotchMedidas.LarguraMaxPermissao - 36, double.PositiveInfinity));
            largura = Math.Min(NotchMedidas.LarguraMaxPermissao, Math.Ceiling(_painelPedido.DesiredSize.Width) + 36);
            altura = NotchMedidas.AlturaPermissao;
            raio = NotchMedidas.RaioPermissao;
        }
        else if (modo == ModoNotch.Aviso)
        {
            _painelAviso.Visibility = Visibility.Visible;
            _painelAviso.Measure(new Size(NotchMedidas.LarguraMaxAviso, double.PositiveInfinity));
            largura = Math.Min(NotchMedidas.LarguraMaxAviso, Math.Ceiling(_painelAviso.DesiredSize.Width));
            altura = NotchMedidas.AlturaAviso;
            raio = NotchMedidas.RaioAviso;
        }
        else
        {
            largura = LarguraNormal + (modo == ModoNotch.Lembrete ? NotchMedidas.ExtraLembrete : 0)
                + (laterais ? 2 * NotchMedidas.LarguraLateral : 0);
            altura = NotchMedidas.AlturaNormal;
            raio = NotchMedidas.RaioNormal;
        }
        largura = Math.Min(largura, NotchMedidas.LarguraMaxCorpo);

        if (laterais) AtualizarEsquerda();
        _ponto.Visibility = modo == ModoNotch.Lembrete ? Visibility.Visible : Visibility.Collapsed;
        if (modo != ModoNotch.Permissao) _pedidoMostrado = null;
        Trocar(_painelPedido, mostrar: modo == ModoNotch.Permissao, animar);
        Trocar(_painelAviso, mostrar: modo == ModoNotch.Aviso, animar);
        Trocar(_linhaRelogio, mostrar: modo is not (ModoNotch.Aviso or ModoNotch.Permissao), animar);
        Trocar(_esquerda, mostrar: laterais, animar);
        Trocar(_direita, mostrar: laterais, animar);

        var duracao = animar ? Efeitos.Tempo("DuracaoNotch", 320) : new Duration(TimeSpan.Zero);
        Animar(LarguraCorpoProperty, largura, duracao);
        Animar(AlturaCorpoProperty, altura, duracao);
        Animar(RaioCorpoProperty, raio, duracao);
    }

    private void AtualizarEsquerda()
    {
        if (_estado.PedidoPerdido)
        {
            _esquerdaTitulo.Text = "Pedido sem resposta";
            _esquerdaSessao.Visibility = Visibility.Collapsed;
        }
        else if (_estado is { Pendente: true, Aviso.Tipo: TipoAviso.Atualizacao })
        {
            _esquerdaTitulo.Text = "Atualização pronta";
            _esquerdaSessao.Visibility = Visibility.Collapsed;
        }
        else if (_estado is { Pendente: true, Aviso: { } a })
        {
            _esquerdaTitulo.Text = a.Tipo == TipoAviso.Terminou ? "Terminou" : "Esperando você";
            _esquerdaSessao.Text = a.Sessao;
            _esquerdaSessao.Visibility = Visibility.Visible;
        }
        else
        {
            _esquerdaTitulo.Text = "Claude";
            _esquerdaSessao.Visibility = Visibility.Collapsed;
        }
    }

    private void Animar(DependencyProperty p, double alvo, Duration duracao) =>
        BeginAnimation(p, new DoubleAnimation(alvo, duracao) { EasingFunction = Efeitos.Mola });

    private static void Trocar(UIElement e, bool mostrar, bool animar)
    {
        if (mostrar) e.Visibility = Visibility.Visible;
        var a = new DoubleAnimation(mostrar ? 1 : 0, animar ? Efeitos.Media : new Duration(TimeSpan.Zero)) { EasingFunction = Efeitos.Suave };
        if (!mostrar) a.Completed += (_, _) => { if (e.Opacity == 0) e.Visibility = Visibility.Collapsed; };
        e.BeginAnimation(OpacityProperty, a);
    }

    private static void Redesenhar(DependencyObject o, DependencyPropertyChangedEventArgs _)
    {
        if (o is not JanelaNotch n) return;
        n._forma.Data = Geometry.Parse(FormaNotch.Caminho(n.LarguraCorpo, n.AlturaCorpo, n.RaioCorpo));
        n._conteudo.Width = n.LarguraCorpo;
        n._conteudo.Height = n.AlturaCorpo;
        n._conteudo.Margin = new Thickness(0, PosicaoIlhas.NotchSobra, 0, 0);
        n._topo.Width = n.LarguraCorpo + 2 * FormaNotch.Orelha;
    }

    private static T Congelado<T>(T f) where T : Freezable { f.Freeze(); return f; }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Vitral.App.Barra;
using Vitral.Ui.Barra;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

public partial class ConteudoDoca : UserControl
{
    private static readonly TimeSpan EsperaMiniaturas = TimeSpan.FromMilliseconds(400), EsperaSair = TimeSpan.FromMilliseconds(300);

    private readonly DispatcherTimer _passar = new() { Interval = EsperaMiniaturas };
    private readonly DispatcherTimer _sair = new() { Interval = EsperaSair };
    private JanelaMiniaturas? _miniaturas;
    private FrameworkElement? _sobre;
    private DocaViewModel? _vm;
    private Point? _inicio;

    public ConteudoDoca()
    {
        InitializeComponent();
        _passar.Tick += (_, _) => { _passar.Stop(); if (_sobre is not null) MostrarMiniaturas(_sobre); };
        _sair.Tick += (_, _) => { _sair.Stop(); if (!IsMouseOver && _miniaturas?.IsMouseOver != true) EsconderMiniaturas(); };
        MouseLeave += (_, _) => _sair.Start();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PediuEscolha -= AoPedirEscolha;
            _vm = DataContext as DocaViewModel;
            if (_vm is not null) _vm.PediuEscolha += AoPedirEscolha;
        };
        Unloaded += (_, _) => { _miniaturas?.Close(); _miniaturas = null; };
    }

    private static BotaoAppViewModel? BotaoDe(object sender) => (sender as FrameworkElement)?.DataContext as BotaoAppViewModel;

    /// Menu do botão direito (fixar, nova janela, fechar).
    private void Botao_Direito(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || BotaoDe(sender) is not { } botao) return;
        EsconderMiniaturas();
        MenuDoca.Abrir((UIElement)sender, _vm.Menu(botao));
        e.Handled = true;
    }

    /// Clique do meio: nova janela.
    private void Botao_Meio(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _vm is null || BotaoDe(sender) is not { } botao) return;
        _vm.NovaJanelaCommand.Execute(botao);
        e.Handled = true;
    }

    // Miniaturas: 400 ms parado sobre um botão com janelas; já abertas, trocam na hora para o botão novo.
    private void Botao_Entrou(object sender, MouseEventArgs e)
    {
        _sobre = sender as FrameworkElement;
        _sair.Stop();
        if (_miniaturas?.Botao is not null && _sobre is not null) MostrarMiniaturas(_sobre);
        else _passar.Start();
    }

    private void Botao_Saiu(object sender, MouseEventArgs e)
    {
        _passar.Stop();
        _sobre = null;
    }

    /// Clique num app com várias janelas.
    private void AoPedirEscolha(BotaoAppViewModel botao)
    {
        var item = ItensDaDoca().FirstOrDefault(f => ReferenceEquals(f.DataContext, botao));
        if (item is not null) MostrarMiniaturas(item);
    }

    private IEnumerable<FrameworkElement> ItensDaDoca()
    {
        foreach (var dado in Itens.Items)
            if (Itens.ItemContainerGenerator.ContainerFromItem(dado) is ContentPresenter c
                && VisualTreeHelperFilho(c) is FrameworkElement f) yield return f;
    }

    private static DependencyObject? VisualTreeHelperFilho(DependencyObject c) =>
        System.Windows.Media.VisualTreeHelper.GetChildrenCount(c) > 0 ? System.Windows.Media.VisualTreeHelper.GetChild(c, 0) : null;

    private void MostrarMiniaturas(FrameworkElement item)
    {
        if (_vm is null || item.DataContext is not BotaoAppViewModel botao) return;
        if (botao.Janelas.Count == 0) { EsconderMiniaturas(); return; }
        if (ReferenceEquals(_miniaturas?.Botao, botao)) return;
        if (Window.GetWindow(this) is not { } doca) return;
        try
        {
            var p = MonitorPrincipal.Ler();
            var monitor = PosicaoIlhas.ParaDips(p.X, p.Y, p.Largura, p.Altura, p.Dpi);
            var retDoca = new Retangulo(doca.Left, doca.Top, doca.ActualWidth, doca.ActualHeight);
            double centro = doca.Left + item.TransformToAncestor(doca).Transform(new Point(item.ActualWidth / 2, 0)).X;
            if (_miniaturas is null)
            {
                _miniaturas = new JanelaMiniaturas();
                _miniaturas.MouseLeave += (_, _) => _sair.Start();
                _miniaturas.MouseEnter += (_, _) => _sair.Stop();
            }
            if (_miniaturas.Botao is { } antigo) antigo.PropertyChanged -= AoMudarBotao;
            _miniaturas.Mostrar(botao, _vm, retDoca, centro, monitor);
            botao.PropertyChanged += AoMudarBotao;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("Doca.Miniaturas", e); }
    }

    /// As janelas do app mudaram com as miniaturas abertas (uma fechou pelo "×", outra abriu): refaz.
    private void AoMudarBotao(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BotaoAppViewModel.Janelas) || s is not BotaoAppViewModel botao || !ReferenceEquals(_miniaturas?.Botao, botao)) return;
        var item = ItensDaDoca().FirstOrDefault(f => ReferenceEquals(f.DataContext, botao));
        botao.PropertyChanged -= AoMudarBotao;
        _miniaturas!.Esconder();
        if (item is not null && botao.Janelas.Count > 0) MostrarMiniaturas(item);
    }

    private void EsconderMiniaturas()
    {
        if (_miniaturas?.Botao is { } b) b.PropertyChanged -= AoMudarBotao;
        _miniaturas?.Esconder();
    }

    // Arrastar para reordenar: só fixados, e só para a posição de outro fixado (DocaViewModel.Mover confere).
    private void Botao_Apertou(object sender, MouseButtonEventArgs e)
    {
        _inicio = e.GetPosition(this);
        _passar.Stop();
        EsconderMiniaturas(); // um clique com várias janelas reabre pelo PediuEscolha
    }

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
        if (_vm is null || e.Data.GetData(typeof(BotaoAppViewModel)) is not BotaoAppViewModel arrastado || BotaoDe(sender) is not { } alvo) return;
        _vm.Mover(_vm.Botoes.IndexOf(arrastado), _vm.Botoes.IndexOf(alvo));
    }
}

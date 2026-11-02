using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Vitral.Ui;

namespace Vitral.App;

/// Diálogos do Windows para a janela principal.
internal sealed class DialogosWpf : IDialogos
{
    public string? EscolherArquivoParaAbrir(string filtro)
    {
        var d = new OpenFileDialog { Filter = filtro };
        return d.ShowDialog() == true ? d.FileName : null;
    }

    public string? EscolherArquivoParaSalvar(string filtro, string nomeSugerido)
    {
        var d = new SaveFileDialog { Filter = filtro, FileName = nomeSugerido };
        return d.ShowDialog() == true ? d.FileName : null;
    }

    public bool Confirmar(string pergunta) =>
        Caixa(pergunta, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public Ui.RespostaPendente PerguntarSalvar(string perfil) =>
        Caixa($"Salvar as mudanças em \"{perfil}\"?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => Ui.RespostaPendente.Salvar,
            MessageBoxResult.No => Ui.RespostaPendente.Descartar,
            _ => Ui.RespostaPendente.Cancelar,
        };

    public void Avisar(string mensagem) =>
        Caixa(mensagem, MessageBoxButton.OK, MessageBoxImage.Information);

    /// Presa à janela principal quando ela está à vista: sem dono, a pergunta pode abrir atrás das outras janelas.
    private static MessageBoxResult Caixa(string texto, MessageBoxButton botoes, MessageBoxImage icone) =>
        Application.Current?.Windows.OfType<JanelaPrincipal>().FirstOrDefault(j => j.IsVisible) is { } dono
            ? MessageBox.Show(dono, texto, "Vitral", botoes, icone)
            : MessageBox.Show(texto, "Vitral", botoes, icone);

    public string? PedirTexto(string pergunta, string sugestao)
    {
        var caixa = new TextBox { Text = sugestao, Margin = new Thickness(0, 8, 0, 12), MinWidth = 280 };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancelar = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 80 };
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        botoes.Children.Add(ok);
        botoes.Children.Add(cancelar);
        var painel = new StackPanel { Margin = new Thickness(16) };
        painel.Children.Add(new TextBlock { Text = pergunta });
        painel.Children.Add(caixa);
        painel.Children.Add(botoes);

        var janela = new Window
        {
            Title = "Vitral", Content = painel, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.Windows.OfType<JanelaPrincipal>().FirstOrDefault(),
        };
        ok.Click += (_, _) => janela.DialogResult = true;
        janela.Loaded += (_, _) => { caixa.Focus(); caixa.SelectAll(); };
        return janela.ShowDialog() == true ? caixa.Text : null;
    }
}

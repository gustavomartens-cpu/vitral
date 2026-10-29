using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Vitral.Core.Atualizacao;
using Vitral.Core.Configuracao;
using Vitral.Ui;

namespace Vitral.App;

public partial class JanelaPrincipal : Window
{
    private readonly Inicializacao _inicializacao;
    private readonly ServicoAtualizacao _atualizacao;
    private readonly string _versao;

    public JanelaPrincipal(PrincipalViewModel vm, Inicializacao inicializacao, ServicoAtualizacao atualizacao, string versao)
    {
        InitializeComponent();
        Vidro.Aplicar(this);
        DataContext = vm;
        // A mensagem só conta os 6 s com a janela à vista.
        IsVisibleChanged += (_, _) => vm.JanelaVisivel = IsVisible;
        _inicializacao = inicializacao;
        _atualizacao = atualizacao;
        _versao = versao;
#if DEBUG
        // F9 simula o aviso de versão pronta no notch (sem a fonte real, reiniciar não faz nada).
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.F9) ((App)Application.Current).AvisarAtualizacao("9.9.9"); };
#endif
    }

    /// A agenda, para a janela de Configurações (avisos e contas).
    internal Agenda.ServicoAgendaApp? Agenda { get; set; }

    /// A engrenagem da barra lateral (spec da interface §3.5).
    private void Configuracoes_Click(object sender, RoutedEventArgs e) =>
        new JanelaConfiguracoes(_inicializacao, _atualizacao, _versao, Agenda) { Owner = this }.ShowDialog();

    /// O "⋯" ao lado de "PERFIS": abre o menu com Importar, Exportar, Desfazer e Voltar ao original.
    private void MaisPerfis_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } botao) return;
        menu.PlacementTarget = botao;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.DataContext = DataContext;
        menu.IsOpen = true;
    }

    /// Trocar de perfil com alteração pendente e escolher Cancelar volta o perfil no editor, mas a lista, no meio da
    /// própria troca, não acompanha a volta: depois que tudo assenta, ela é posta de novo no perfil do editor.
    private void ListaPerfis_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            // Compara pelo item destacado: o SelectedValue já volta, mas o destaque fica no perfil clicado.
            if (DataContext is not PrincipalViewModel vm
                || string.Equals((ListaPerfis.SelectedItem as ItemPerfil)?.Nome, vm.Selecionado, StringComparison.OrdinalIgnoreCase)) return;
            ListaPerfis.SelectedItem = vm.ItensPerfis.FirstOrDefault(i => string.Equals(i.Nome, vm.Selecionado, StringComparison.OrdinalIgnoreCase));
        }, System.Windows.Threading.DispatcherPriority.ContextIdle);

    /// Aba Aparência: a transparência volta a "do tema" (o deslizante não tem como mostrar nulo).
    private void TransparenciaDoTema_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is EditorAjustesAparencia campos) campos.Transparencia = null;
    }

    /// Fechar a janela só esconde: o Vitral continua na bandeja. "Sair" fica no menu da bandeja.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

using System.ComponentModel;
using System.Windows;
using Vitral.App.Barra;
using Vitral.Core.Aplicacao;
using Vitral.Core.Barra;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;
using Vitral.Core.Windows;
using Vitral.Ui;

namespace Vitral.App;

/// Ponto de montagem: liga o núcleo ao Windows real e abre a janela.
public partial class App : Application
{
    private JanelaPrincipal? _janela;
    private JanelaContagem? _janelaContagem;

    internal PrincipalViewModel Principal { get; private set; } = null!;
    internal ServicoVitral Servico { get; private set; } = null!;
    internal Inicializacao Inicializacao { get; private set; } = null!;

    private InstanciaUnica? _instancia;
    private NotificadorEmSegundoPlano? _notificador;
    private Bandeja? _bandeja;
    private ControleBarra? _barra;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += AoErroNaoTratado;

        _instancia = new InstanciaUnica(() => Dispatcher.BeginInvoke(MostrarJanela));
        if (!_instancia.EhPrimeira)
        {
            Shutdown(); // a outra instância vai mostrar a janela
            return;
        }

        var registro = new RegistroWindows();
        var config = new ServicoConfig(System.IO.Path.Combine(Pastas.Raiz, "config.json"));
        _barra = new ControleBarra(m => _bandeja?.Avisar("Vitral", m));
        var estadoBarra = new EstadoBarra(_barra, config);
        var retratos = new ServicoRetratos(registro, Pastas.Retratos, estadoBarra.Atual);
        var perfis = new RepositorioPerfis(Pastas.Perfis);
        _notificador = new NotificadorEmSegundoPlano(new NotificadorWindows()); // ver comentário da classe
        Servico = new ServicoVitral(new Aplicador(registro, retratos, _notificador, estadoBarra), retratos, perfis, config);
        Inicializacao = new Inicializacao(registro);

        bool peloWindows = e.Args.Contains(Vitral.Core.Configuracao.Inicializacao.Argumento);
        var estado = Servico.AoIniciar(peloWindows);
        _barra.Aplicar(Servico.BarraAtual()); // volta como estava (no login, o perfil ativo acabou de ser reaplicado)
        // Monitores, resolução ou escala mudaram, ou o PC voltou da suspensão: reposiciona e relê a barra.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(() => _barra.Reajustar());
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, p) =>
        {
            if (p.Mode == Microsoft.Win32.PowerModes.Resume) Dispatcher.BeginInvoke(() => _barra.Reajustar());
        };

        Principal = new PrincipalViewModel(Servico, perfis, new DialogosWpf(), new RelogioWpf());
        Principal.PropertyChanged += AoMudarPrincipal;
        if (estado.TesteCancelado) Principal.Mensagem = "Um teste ficou aberto da última vez; o visual voltou ao anterior.";
        if (estado.Aviso is not null) Principal.Mensagem = estado.Aviso;

        _bandeja = new Bandeja(this);
        if (!peloWindows) MostrarJanela();
        else if (estado.Aviso is not null || estado.TesteCancelado) _bandeja.Avisar("Vitral", Principal.Mensagem ?? "");
    }

    internal void MostrarJanela()
    {
        _janela ??= new JanelaPrincipal(Principal, Inicializacao);
        _janela.Show();
        if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    }

    internal void Sair()
    {
        // Sair no meio de um teste volta ao anterior, como se a contagem tivesse acabado.
        Principal.Contagem?.VoltarCommand.Execute(null);
        _janelaContagem?.Close();
        _barra?.Fechar();
        _notificador?.Aguardar(TimeSpan.FromSeconds(5));
        _bandeja?.Dispose();
        _instancia?.Dispose();
        Shutdown();
    }

    /// Rede de segurança: um erro inesperado na interface vira aviso e log, em vez de derrubar o app
    /// (com um teste em andamento, cair deixaria o visual de teste aplicado até a próxima abertura).
    private void AoErroNaoTratado(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Pastas.Logs);
            System.IO.File.AppendAllText(System.IO.Path.Combine(Pastas.Logs, "app-erros.log"), $"{DateTimeOffset.Now:O} {e.Exception}{Environment.NewLine}");
        }
        catch { /* sem log, o aviso abaixo ainda aparece */ }
        MessageBox.Show($"Algo deu errado: {e.Exception.Message}\n\nDetalhes em {Pastas.Logs}.", "Vitral",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    /// A contagem do "Testar" aparece numa janelinha sempre visível, no canto da tela.
    private void AoMudarPrincipal(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PrincipalViewModel.Contagem)) return;
        _janelaContagem?.Close();
        _janelaContagem = null;
        if (Principal.Contagem is { } contagem)
        {
            _janelaContagem = new JanelaContagem(contagem);
            _janelaContagem.Show();
        }
    }
}

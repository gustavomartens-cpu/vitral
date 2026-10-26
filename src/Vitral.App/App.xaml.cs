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
    private BarraDeTarefas.ObservadorIcones? _observadorIcones;
    private EstadoBarra? _estadoBarra;
    private EstadoDoca? _estadoDoca;
    private Doca.ControleDoca? _doca;
    private Doca.BarraDoWindows? _barraWindows;
    private Doca.FonteBandeja? _bandejaApps;
    private Claude.CanalAvisos? _canalAvisos;
    private System.Windows.Threading.DispatcherTimer? _relogioAtualizacao;

    internal Vitral.Core.Atualizacao.ServicoAtualizacao Atualizador { get; private set; } = null!;
    internal string VersaoAtual { get; private set; } = "?";

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
        _notificador = new NotificadorEmSegundoPlano(new NotificadorWindows()); // ver comentário da classe
        _bandejaApps = new Doca.FonteBandeja(Dispatcher);
        _barra = new ControleBarra(m => _bandeja?.Avisar("Vitral", m), _bandejaApps);
        _estadoBarra = new EstadoBarra(_barra, config, registro, _notificador); // também cuida do relógio de baixo
        var estadoIcones = new EstadoIcones(config);
        _barraWindows = new Doca.BarraDoWindows(registro, _notificador);
        _doca = new Doca.ControleDoca(_barraWindows, _bandejaApps, m => _bandeja?.Avisar("Vitral", m));
        _estadoDoca = new EstadoDoca(_doca, _barraWindows, config);
        // Travamento fatal com a doca ligada: a barra do Windows volta antes de o processo sumir.
        // Na thread da interface também devolve os ícones da bandeja (a janela da ManagedShell morre com o processo).
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            _barraWindows?.Mostrar();
            if (Dispatcher.CheckAccess())
                try { _bandejaApps?.Desligar(); } catch (Exception) { /* processo caindo: melhor esforço */ }
        };
        // Sair da conta ou desligar o Windows: mesma limpeza do Sair (barra do Windows e ocultar automático de volta).
        SessionEnding += (_, _) => Sair();
        var retratos = new ServicoRetratos(registro, Pastas.Retratos, _estadoBarra.Atual, estadoIcones.Atual, _estadoDoca.Atual);
        var perfis = new RepositorioPerfis(Pastas.Perfis);
        Servico = new ServicoVitral(new Aplicador(registro, retratos, _notificador, _estadoBarra, estadoIcones, _estadoDoca), retratos, perfis, config);
        Inicializacao = new Inicializacao(registro);

        bool peloWindows = e.Args.Contains(Vitral.Core.Configuracao.Inicializacao.Argumento);
        var estado = Servico.AoIniciar(peloWindows);
        _estadoBarra.Mostrar(); // volta como estava (no login, o perfil ativo acabou de ser reaplicado)
        _estadoDoca.AoIniciar(); // liga a doca se estava ligada; senão garante a barra do Windows
        // Avisos do Claude Code (spec do notch vivo §4.4)
        _canalAvisos = new Claude.CanalAvisos(Dispatcher, a => _barra?.Avisar(a),
            p => _barra?.Pedir(p) ?? Task.FromResult<Core.Claude.EscolhaPermissao?>(null), id => _barra?.Desistir(id));
        // Atualização automática pelas releases do GitHub (spec da atualização §5): confere 1 min depois de abrir e a
        // cada 6 h; a versão baixada é avisada no notch e entra com o clique (ou na próxima vez que o Vitral abrir).
        var fonte = new Atualizacao.FonteVelopack(Vitral.Core.Atualizacao.TokenEmbutido.Ler(typeof(App).Assembly), LimparParaSair);
        VersaoAtual = fonte.VersaoAtual;
        Atualizador = new Vitral.Core.Atualizacao.ServicoAtualizacao(fonte, () => DateTime.Now);
        Atualizador.Pronta += AvisarAtualizacao;
        _barra.Reiniciar = Atualizador.Reiniciar;
        _relogioAtualizacao = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogioAtualizacao.Tick += async (_, _) => await Atualizador.TiqueAsync();
        _relogioAtualizacao.Start();
        // Depois de AoIniciar: um teste que ficou aberto já voltou, e o observador começa com a lista certa.
        _observadorIcones = new BarraDeTarefas.ObservadorIcones(registro, estadoIcones, _notificador);
        // Monitores, resolução ou escala mudaram, ou o PC voltou da suspensão: reposiciona e relê a barra.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(() => { _barra.Reajustar(); _doca.Reajustar(); });
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, p) =>
        {
            if (p.Mode == Microsoft.Win32.PowerModes.Resume) Dispatcher.BeginInvoke(() => { _barra.Reajustar(); _doca.Reajustar(); });
        };

        Principal = new PrincipalViewModel(Servico, perfis, new DialogosWpf(), new RelogioWpf(), () => Icones.Programas(registro));
        Principal.PropertyChanged += AoMudarPrincipal;
        if (estado.TesteCancelado) Principal.Mensagem = "Um teste ficou aberto da última vez; o visual voltou ao anterior.";
        if (estado.Aviso is not null) Principal.Mensagem = estado.Aviso;

        _bandeja = new Bandeja(this);
        if (!peloWindows) MostrarJanela();
        else if (estado.Aviso is not null || estado.TesteCancelado) _bandeja.Avisar("Vitral", Principal.Mensagem ?? "");
    }

    internal void MostrarJanela()
    {
        _janela ??= new JanelaPrincipal(Principal, Inicializacao, Atualizador, VersaoAtual);
        _janela.Show();
        if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    }

    /// Versão nova baixada: aviso no notch (sem a barra do topo, fica só a linha de estado na janela).
    internal void AvisarAtualizacao(string versao) =>
        _barra?.Avisar(new Vitral.Core.Claude.AvisoClaude(Vitral.Core.Claude.TipoAviso.Atualizacao, "Vitral", $"{versao} pronto"));

    internal void Sair()
    {
        LimparParaSair();
        Shutdown();
    }

    /// Tudo o que o Sair desfaz antes de fechar (também usado antes de reiniciar para atualizar).
    private void LimparParaSair()
    {
        _relogioAtualizacao?.Stop();
        // Sair no meio de um teste volta ao anterior, como se a contagem tivesse acabado.
        Principal.Contagem?.VoltarCommand.Execute(null);
        _janelaContagem?.Close();
        _barra?.Fechar();
        _estadoBarra?.Encerrar(); // sem a barra do topo, o relógio de baixo volta
        _estadoDoca?.Encerrar(); // sem a doca, a barra do Windows volta
        _barraWindows?.Dispose();
        _canalAvisos?.Dispose();
        _bandejaApps?.Dispose(); // devolve os ícones ao Windows
        _observadorIcones?.Dispose();
        _notificador?.Aguardar(TimeSpan.FromSeconds(5));
        _bandeja?.Dispose();
        _instancia?.Dispose();
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

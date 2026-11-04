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
    private Agenda.ServicoAgendaApp? _agenda; // spec da agenda
    private Vitral.App.Notificacoes.WindowsQuieto? _quieto;

    /// As notificações dos outros apps (spec das notificações).
    internal Vitral.App.Notificacoes.ServicoNotificacoes? ServicoDeNotificacoes { get; private set; }
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

        // Identidade de pacote para ler as notificações: registrada agora, só vale num processo novo.
        if (Vitral.App.Notificacoes.PacoteVitral.RegistrarEReabrir(e.Args)) { Shutdown(); return; }
        Barra.LogBarra.Info($"Identidade de pacote: {(Vitral.App.Notificacoes.PacoteVitral.TemIdentidade ? "sim" : "não")}");

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
        _agenda = new Agenda.ServicoAgendaApp(config);
        _barra.Agenda = _agenda;
        _barra.Controles = new Controles.FonteControlesWindows(config); // spec dos controles
        _agenda.Lembrar += a => _barra?.Lembrar(a);
        _agenda.Sumiu += id => _barra?.SumiuDaAgenda(id);
        _estadoBarra = new EstadoBarra(_barra, config, registro, _notificador); // também cuida do relógio de baixo
        var estadoIcones = new EstadoIcones(config);
        _barraWindows = new Doca.BarraDoWindows(registro, _notificador);
        _doca = new Doca.ControleDoca(_barraWindows, _bandejaApps, m => _bandeja?.Avisar("Vitral", m));
        _estadoDoca = new EstadoDoca(_doca, _barraWindows, config);
        var estadoAparencia = new Core.Aparencia.EstadoAparencia(Tema.ServicoAparencia.Instancia, config); // spec da aparência §5
        var estadoFormas = new Core.Formas.EstadoFormas(Tema.ServicoFormas.Instancia, config); // spec das formas §5.1
        ServicoDoIniciar = new Vitral.App.Iniciar.ServicoIniciar(registro, _notificador); // spec do Iniciar §4.2
        var estadoIniciar = new Core.Iniciar.EstadoIniciar(ServicoDoIniciar, config);
        // Notificações no notch (spec das notificações): o balão do Windows calado de uma vez anterior (uma queda) volta
        // agora; o serviço cala de novo quando estiver lendo.
        _quieto = new Vitral.App.Notificacoes.WindowsQuieto(config);
        _quieto.Devolver();
        ServicoDeNotificacoes = new Vitral.App.Notificacoes.ServicoNotificacoes(Dispatcher, _quieto);
        ServicoDeNotificacoes.Novas += novas => Barra.LogBarra.Info($"Notificações novas: {novas.Count}");
        ServicoDeNotificacoes.NotchNaTela = () => _barra?.TemNotch == true;
        _barra.Notificacoes = ServicoDeNotificacoes; // o sino, os avisos e o painel do notch
        // Travamento fatal com a doca ligada: a barra do Windows volta antes de o processo sumir.
        // Na thread da interface também devolve os ícones da bandeja (a janela da ManagedShell morre com o processo).
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            _quieto?.Encerrar();
            _barraWindows?.Mostrar();
            if (Dispatcher.CheckAccess())
                try { _bandejaApps?.Desligar(); } catch (Exception) { /* processo caindo: melhor esforço */ }
        };
        // Sair da conta ou desligar o Windows: mesma limpeza do Sair (barra do Windows e ocultar automático de volta).
        SessionEnding += (_, _) => Sair();
        var retratos = new ServicoRetratos(registro, Pastas.Retratos, _estadoBarra.Atual, estadoIcones.Atual, _estadoDoca.Atual, estadoAparencia.Atual, estadoFormas.Atual,
            estadoIniciar.AtualPerfil);
        var perfis = new RepositorioPerfis(Pastas.Perfis);
        Servico = new ServicoVitral(new Aplicador(registro, retratos, _notificador, _estadoBarra, estadoIcones, _estadoDoca, estadoAparencia, estadoFormas, estadoIniciar), retratos, perfis, config);
        Inicializacao = new Inicializacao(registro);

        bool peloWindows = e.Args.Contains(Vitral.Core.Configuracao.Inicializacao.Argumento);
        var estado = Servico.AoIniciar(peloWindows);
        estadoAparencia.AoIniciar(); // antes das janelas: elas já nascem com a aparência e as formas guardadas
        estadoFormas.AoIniciar();
        estadoIniciar.AoIniciar(); // depois da aparência: as cores do Iniciar vêm da doca
        _estadoBarra.Mostrar(); // volta como estava (no login, o perfil ativo acabou de ser reaplicado)
        _estadoDoca.AoIniciar(); // liga a doca se estava ligada; senão garante a barra do Windows
        _ = ServicoDeNotificacoes.Iniciar(); // pede a permissão na primeira vez
        _ = _agenda.AtualizarAgora(); // a primeira leitura da agenda (depois, a cada 5 min)
        // Avisos do Claude Code (spec do notch vivo §4.4); ganchos de uma versão anterior ganham o tempo novo das perguntas
        new Claude.ServicoGanchosClaude().AtualizarSeLigado();
        _canalAvisos = new Claude.CanalAvisos(Dispatcher, a => _barra?.Avisar(a),
            p => _barra?.Pedir(p) ?? Task.FromResult<Core.Claude.EscolhaPermissao?>(null),
            r => _barra?.Responder(r) ?? Task.FromResult<string?>(null), id => _barra?.Desistir(id));
        // Atualização automática pelas releases do GitHub (spec da atualização §5): confere 1 min depois de abrir e a
        // cada 6 h; a versão baixada é avisada no notch e entra com o clique (ou na próxima vez que o Vitral abrir).
        var fonte = new Atualizacao.FonteVelopack(Vitral.Core.Atualizacao.TokenEmbutido.Ler(typeof(App).Assembly), LimparParaSair);
        VersaoAtual = fonte.VersaoAtual;
        Atualizador = new Vitral.Core.Atualizacao.ServicoAtualizacao(fonte, () => DateTime.Now);
        Atualizador.Pronta += AvisarAtualizacao;
        Atualizador.PodeReiniciar = PodeFechar; // pelo notch e pela linha de Configurações
        _barra.Reiniciar = Atualizador.Reiniciar;
        _relogioAtualizacao = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogioAtualizacao.Tick += async (_, _) =>
        {
            await Atualizador.TiqueAsync();
            // Mantém ligada a acessibilidade do app do Claude: o gancho lê por ela que conversa está à vista, e o
            // Chromium só monta essa leitura depois que alguém pergunta.
            Claude.JanelasDoClaude.Aquecer();
        };
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
        Principal.EditorAparencia.LerWindows = () => Tema.ServicoAparencia.Instancia.VisualDoWindows;
        Principal.CantosEmVigor = () => Tema.ServicoAparencia.Instancia.Valores(Tema.ParteAparencia.Notch).Cantos;
        // O Windows mudou (modo, destaque) ou outro perfil foi aplicado: a prévia da aba Aparência acompanha.
        Tema.ServicoAparencia.Instancia.Mudou += Principal.EditorAparencia.Recalcular;
        // O Iniciar: a linha de estado acompanha o serviço, e o botão da reserva liga a opção nativa.
        Principal.EditorIniciar.Estado = ServicoDoIniciar.Estado;
        ServicoDoIniciar.EstadoMudou += m => Principal.EditorIniciar.Estado = m;
        Principal.EditorIniciar.PediuDestaqueDoWindows += ServicoDoIniciar.UsarDestaqueDoWindows;
        Principal.EditorFormas.Atuais = estadoFormas.Atual; // "Não mexer" mantém o que está: a prévia mostra isso
        Tema.ServicoFormas.Instancia.Mudou += Principal.EditorFormas.Recalcular;
        Principal.EditorFormas.Recalcular();
        Principal.PropertyChanged += AoMudarPrincipal;
        if (estado.TesteCancelado) Principal.Mensagem = "Um teste ficou aberto da última vez; o visual voltou ao anterior.";
        if (estado.Aviso is not null) Principal.Mensagem = estado.Aviso;

        _bandeja = new Bandeja(this);
        if (!peloWindows) MostrarJanela();
        else if (estado.Aviso is not null || estado.TesteCancelado) _bandeja.Avisar("Vitral", Principal.Mensagem ?? "");
    }

    /// Antes de sair ou reiniciar para atualizar: com edição pendente, a janela aparece primeiro, para a pergunta de
    /// salvar abrir presa a ela (e não perdida atrás das outras) e mostrar o que está pendente.
    internal bool PodeFechar()
    {
        if (Principal.TemAlteracoes) MostrarJanela();
        return Principal.PodeSair();
    }

    internal void MostrarJanela()
    {
        _janela ??= new JanelaPrincipal(Principal, Inicializacao, Atualizador, VersaoAtual) { Agenda = _agenda };
        _janela.Show();
        if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    }

    /// Versão nova baixada: aviso no notch (sem a barra do topo, fica só a linha de estado na janela).
    internal void AvisarAtualizacao(string versao) =>
        _barra?.Avisar(new Vitral.Core.Claude.AvisoClaude(Vitral.Core.Claude.TipoAviso.Atualizacao, "Vitral", $"{versao} pronto"));

    /// O Iniciar com o visual da doca (spec do Iniciar §4.2).
    internal Vitral.App.Iniciar.ServicoIniciar? ServicoDoIniciar { get; private set; }

    internal void Sair()
    {
        LimparParaSair();
        Shutdown();
    }

    /// Tudo o que o Sair desfaz antes de fechar (também usado antes de reiniciar para atualizar).
    private void LimparParaSair()
    {
        // O balão do Windows volta primeiro, e nada mais o cala: um passo que falhe depois não pode deixá-lo calado.
        Passo("Notificacoes", () => ServicoDeNotificacoes?.Dispose());
        Passo("Quieto", () => _quieto?.Encerrar());
        Passo("Atualizacao", () => _relogioAtualizacao?.Stop());
        // Sair no meio de um teste volta ao anterior, como se a contagem tivesse acabado.
        Passo("Contagem", () => Principal.Contagem?.VoltarCommand.Execute(null));
        Passo("JanelaContagem", () => _janelaContagem?.Close());
        Passo("Barra", () => _barra?.Fechar());
        Passo("EstadoBarra", () => _estadoBarra?.Encerrar()); // sem a barra do topo, o relógio de baixo volta
        Passo("EstadoDoca", () => _estadoDoca?.Encerrar()); // sem a doca, a barra do Windows volta
        Passo("BarraWindows", () => _barraWindows?.Dispose());
        Passo("CanalAvisos", () => _canalAvisos?.Dispose());
        Passo("BandejaApps", () => _bandejaApps?.Dispose()); // devolve os ícones ao Windows
        Passo("Iniciar", () => ServicoDoIniciar?.Dispose()); // o Iniciar volta ao do Windows quando o processo sai
        Passo("ObservadorIcones", () => _observadorIcones?.Dispose());
        Passo("Notificador", () => _notificador?.Aguardar(TimeSpan.FromSeconds(5)));
        Passo("Bandeja", () => _bandeja?.Dispose());
        Passo("Instancia", () => _instancia?.Dispose());
    }

    /// Um passo da saída: a falha de um vai para o log e não impede os seguintes de devolverem o Windows.
    private static void Passo(string nome, Action fazer)
    {
        try { fazer(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro($"Sair.{nome}", e); }
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
        e.Handled = true;
        // Fila de mensagens cheia (ERROR_NOT_ENOUGH_QUOTA): o WPF falha ao se avisar de uma janela oculta e se recupera
        // sozinho quando a fila esvazia. A caixa de aviso só travaria mais a fila; fica o log.
        if (e.Exception is System.ComponentModel.Win32Exception { NativeErrorCode: 1816 }) return;
        MessageBox.Show($"Algo deu errado: {e.Exception.Message}\n\nDetalhes em {Pastas.Logs}.", "Vitral",
            MessageBoxButton.OK, MessageBoxImage.Warning);
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

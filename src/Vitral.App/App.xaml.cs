using System.ComponentModel;
using System.Windows;
using Vitral.Core.Aplicacao;
using Vitral.Core.Configuracao;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var registro = new RegistroWindows();
        var retratos = new ServicoRetratos(registro, Pastas.Retratos);
        var perfis = new RepositorioPerfis(Pastas.Perfis);
        Servico = new ServicoVitral(new Aplicador(registro, retratos, new NotificadorWindows()), retratos, perfis,
            new ServicoConfig(System.IO.Path.Combine(Pastas.Raiz, "config.json")));
        Inicializacao = new Inicializacao(registro);

        bool peloWindows = e.Args.Contains(Vitral.Core.Configuracao.Inicializacao.Argumento);
        var estado = Servico.AoIniciar(peloWindows);

        Principal = new PrincipalViewModel(Servico, perfis, new DialogosWpf(), new RelogioWpf());
        Principal.PropertyChanged += AoMudarPrincipal;
        if (estado.TesteCancelado) Principal.Mensagem = "Um teste ficou aberto da última vez; o visual voltou ao anterior.";
        if (estado.Aviso is not null) Principal.Mensagem = estado.Aviso;

        if (!peloWindows) MostrarJanela();
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
        _janelaContagem?.Close();
        Shutdown();
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

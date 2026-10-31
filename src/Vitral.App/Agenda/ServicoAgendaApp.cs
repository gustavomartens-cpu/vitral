using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using Microsoft.Win32;
using Vitral.App.Barra;
using Vitral.Core.Agenda;
using Vitral.Core.Configuracao;
using Vitral.Core.Windows;
using Vitral.Ui.Barra;

namespace Vitral.App.Agenda;

/// A agenda no Vitral (spec da agenda §5.2): monta as fontes a partir das contas do config, lê a cada 5 min, ao voltar da
/// suspensão e quando pedem, e confere os avisos a cada 30 s.
internal sealed class ServicoAgendaApp
{
    private readonly ServicoConfig _config;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly LoginGoogle _google;
    private readonly LoginMicrosoft _microsoft;
    private readonly DispatcherTimer _leitura = new() { Interval = TimeSpan.FromMinutes(5) };
    private readonly DispatcherTimer _avisos = new() { Interval = TimeSpan.FromSeconds(30) };

    public ServicoAgendaApp(ServicoConfig config)
    {
        _config = config;
        var cofre = new CofreTokens(Path.Combine(Pastas.Raiz, "contas"));
        _google = new LoginGoogle(cofre, _http);
        _microsoft = new LoginMicrosoft(cofre);
        Agenda.Mudou += VerSumidos;
        Montar();
        _leitura.Tick += async (_, _) => await AtualizarAgora();
        _avisos.Tick += (_, _) => ConferirAvisos();
        _leitura.Start();
        _avisos.Start();
        SystemEvents.PowerModeChanged += (_, e) =>
        {
            if (e.Mode == PowerModes.Resume) Dispatcher.CurrentDispatcher.BeginInvoke(async () => await AtualizarAgora());
        };
    }

    public ServicoAgenda Agenda { get; } = new(() => DateTimeOffset.Now, TimeZoneInfo.Local);

    /// Um compromisso entrou na antecedência (o notch avisa).
    public event Action<AvisoAgenda>? Lembrar;

    /// Um compromisso avisado saiu da agenda (cancelado): o lembrete some.
    public event Action<string>? Sumiu;

    public AgendaConfig Config => _config.Ler().Agenda ?? new AgendaConfig([]);

    public static bool Disponivel(ProvedorAgenda p) => p == ProvedorAgenda.Google ? Credenciais.GoogleOk : Credenciais.MicrosoftOk;

    public async Task AtualizarAgora()
    {
        try { await Agenda.Atualizar(CancellationToken.None); }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ServicoAgendaApp.Atualizar", e); }
        ConferirAvisos();
    }

    /// Liga uma conta: abre o navegador; devolve null se deu certo ou a pessoa desistiu, ou o texto do erro.
    public async Task<string?> Entrar(ProvedorAgenda provedor, CancellationToken cancelar = default)
    {
        if (!Disponivel(provedor)) return "Falta cadastrar o app (docs/agenda-cadastro.md).";
        string? email;
        try { email = provedor == ProvedorAgenda.Google ? await _google.Entrar(cancelar) : await _microsoft.Entrar(cancelar); }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException) { return e.Message; }
        if (email is null) return null;
        var cfg = Config;
        // A mesma conta ligada de novo substitui a anterior (spec §6).
        var outras = cfg.Contas.Where(c => !(c.Provedor == provedor && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase))).ToList();
        var conta = new ContaAgenda(provedor, email, RegrasAgenda.CorDaConta(provedor, outras.Count(c => c.Provedor == provedor)));
        Gravar(cfg with { Contas = [.. outras, conta] });
        await AtualizarAgora();
        return null;
    }

    public async Task Remover(ContaAgenda conta)
    {
        try
        {
            if (conta.Provedor == ProvedorAgenda.Google) await _google.Sair(conta.Email);
            else await _microsoft.Sair(conta.Email);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ServicoAgendaApp.Remover", e); }
        var cfg = Config.ComAgendas(conta, []); // as agendas escolhidas saem junto
        Gravar(cfg with { Contas = cfg.Contas.Where(c => c != conta).ToList() });
    }

    /// As agendas da conta do Google, para a janela de escolha. Erro sobe para a janela mostrar.
    public Task<IReadOnlyList<AgendaGoogle>> AgendasDoGoogle(ContaAgenda conta) =>
        LeitorGoogle.ListarAgendas(_http, _google.Token(conta.Email), CancellationToken.None);

    /// Grava as agendas escolhidas além da principal e lê de novo.
    public async Task DefinirAgendas(ContaAgenda conta, IEnumerable<string> ids)
    {
        Gravar(Config.ComAgendas(conta, ids));
        await AtualizarAgora();
    }

    public void DefinirAntecedencia(int minutos) => Gravar(Config with { AntecedenciaMin = minutos }, remontar: false);

    public void DefinirAvisar(bool avisar) => Gravar(Config with { Avisar = avisar }, remontar: false);

    private void Gravar(AgendaConfig agenda, bool remontar = true)
    {
        _config.Gravar(_config.Ler() with { Agenda = agenda });
        if (remontar) Montar();
    }

    private void Montar()
    {
        var cfg = Config;
        Agenda.DefinirFontes(cfg.Contas.Select(c => Fonte(c, cfg)).OfType<IFonteAgenda>().ToList());
    }

    private IFonteAgenda? Fonte(ContaAgenda c, AgendaConfig cfg) => c.Provedor switch
    {
        ProvedorAgenda.Google when Credenciais.GoogleOk => new LeitorGoogle(c, _http, _google.Token(c.Email), null, cfg.AgendasDe(c)),
        ProvedorAgenda.Microsoft when Credenciais.MicrosoftOk => new LeitorMicrosoft(c, _http, _microsoft.Token(c.Email)),
        _ => null,
    };

    private void ConferirAvisos()
    {
        var cfg = Config;
        if (!cfg.Avisar) return;
        var antecedencia = TimeSpan.FromMinutes(cfg.AntecedenciaMin);
        foreach (var c in Agenda.AvisosParaMostrar(antecedencia)) Lembrar?.Invoke(new AvisoAgenda(c, antecedencia));
    }

    /// Avisado que saiu da agenda ou mudou de horário: o lembrete dele sai (o horário novo avisa de novo).
    private void VerSumidos()
    {
        foreach (var id in Agenda.AvisadosQueSumiram()) Sumiu?.Invoke(id);
    }
}

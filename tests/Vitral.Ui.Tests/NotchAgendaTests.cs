using Vitral.Core.Agenda;
using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class NotchAgendaTests
{
    private DateTime _agora = new(2026, 10, 28, 14, 20, 0);
    private EstadoNotch Criar() => new(() => _agora);
    private void Avancar(double segundos, EstadoNotch e) { _agora = _agora.AddSeconds(segundos); e.Tique(); }

    private static readonly ContaAgenda Google = new(ProvedorAgenda.Google, "g@x.com", "#4C7DF0");

    private AvisoAgenda Reuniao(string id = "r1", double minutosAte = 10) => new(new Compromisso(id, Google, "Reunião com cliente",
        new DateTimeOffset(_agora.AddMinutes(minutosAte)), new DateTimeOffset(_agora.AddMinutes(minutosAte + 60)), false, null,
        new Uri("https://meet.google.com/abc-defg-hij"), null, false, false), TimeSpan.FromMinutes(10));

    [Fact]
    public void Aviso_da_agenda_vira_lembrete_depois_de_6s()
    {
        var e = Criar();
        var r = Reuniao();
        e.Lembrou(r);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Assert.True(e.AvisoEhDaAgenda);
        Assert.Equal(r, e.AvisoAgendaAtual);
        Avancar(6.1, e);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
        Assert.Equal(r, e.AvisoAgendaAtual);
    }

    [Fact]
    public void Lembrete_fica_ate_o_fim()
    {
        var e = Criar();
        e.Lembrou(Reuniao(minutosAte: 10));
        Avancar(7, e);
        Avancar(60 * 69, e); // ainda dentro da reunião
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
        Avancar(60 * 2, e); // acabou
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.Null(e.AvisoAgendaAtual);
    }

    [Fact]
    public void Permissao_passa_na_frente_e_o_aviso_espera()
    {
        var e = Criar();
        e.Pediu(new PedidoPermissao("p1", "vitral", "Bash", "Claude quer rodar um comando", "ls"));
        e.Lembrou(Reuniao());
        Assert.Equal(ModoNotch.Permissao, e.Modo);
        e.Desistiu("p1");
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Assert.True(e.AvisoEhDaAgenda);
    }

    [Fact]
    public void Avisos_da_agenda_e_do_Claude_em_fila_por_chegada()
    {
        var e = Criar();
        e.Chegou(new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto."));
        e.Lembrou(Reuniao());
        Assert.False(e.AvisoEhDaAgenda); // o do Claude continua na tela
        Avancar(6.1, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo); // agora o da agenda
        Assert.True(e.AvisoEhDaAgenda);
        e.Lembrou(Reuniao("r2"));
        Avancar(6.1, e);
        Assert.Equal("r2", e.AvisoAgendaAtual!.Compromisso.Id);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Clique_abre_o_calendario_no_dia()
    {
        var e = Criar();
        e.Lembrou(Reuniao());
        Assert.Equal(AcaoNotch.AbrirCalendario, e.Clicou());
        Assert.Equal(new DateOnly(2026, 10, 28), e.DiaPedido);
        Assert.Null(e.AvisoAgendaAtual);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Entrar_devolve_o_link_e_limpa()
    {
        var e = Criar();
        e.Lembrou(Reuniao());
        Assert.Equal("https://meet.google.com/abc-defg-hij", e.EntrarNaReuniao()?.ToString());
        Assert.Null(e.AvisoAgendaAtual);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Lembrete_some_quando_o_compromisso_some()
    {
        var e = Criar();
        e.Lembrou(Reuniao());
        Avancar(7, e);
        e.Sumiu("r1");
        Assert.Null(e.AvisoAgendaAtual);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Lembrete_do_Claude_continua_quando_o_da_agenda_sai()
    {
        var e = Criar();
        e.Chegou(new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto."));
        Avancar(6.1, e);
        e.Lembrou(Reuniao());
        Avancar(6.1, e);
        e.Sumiu("r1");
        Assert.Equal(ModoNotch.Lembrete, e.Modo); // o do Claude ainda está pendente
        Assert.True(e.Pendente);
    }
}

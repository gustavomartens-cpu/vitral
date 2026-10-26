using System.Globalization;
using System.Text.RegularExpressions;
using Vitral.Core.Claude;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class NotchTests
{
    private DateTime _agora = new(2026, 10, 25, 21, 0, 0);
    private EstadoNotch Criar() => new(() => _agora);
    private void Avancar(double segundos, EstadoNotch e) { _agora = _agora.AddSeconds(segundos); e.Tique(); }

    private static readonly AvisoClaude A1 = new(TipoAviso.Terminou, "vitral", "Pronto.");
    private static readonly AvisoClaude A2 = new(TipoAviso.Esperando, "sic", "Posso seguir?");

    [Fact]
    public void Caminho_comeca_no_topo_e_fecha()
    {
        var c = FormaNotch.Caminho(200, 34, 16);
        Assert.StartsWith("M0,0", c);
        Assert.EndsWith("Z", c);
        Assert.Contains("A16,16", c);
        Assert.Contains("A22,22", FormaNotch.Caminho(300, 52, 22));
    }

    [Fact]
    public void Caminho_tem_a_largura_total()
    {
        var xs = Regex.Matches(FormaNotch.Caminho(200, 34, 16), @"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)")
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        Assert.Contains(232d, xs);
        Assert.True(xs.Max() <= 232);
    }

    [Fact]
    public void Aviso_depois_de_6s_vira_lembrete()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(5.9, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Avancar(0.2, e);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
    }

    [Fact]
    public void Clique_no_aviso_abre_o_Claude_e_volta_ao_normal()
    {
        var e = Criar();
        e.Chegou(A1);
        Assert.Equal(AcaoNotch.AbrirClaude, e.Clicou());
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Avancar(10, e);
        Assert.Equal(ModoNotch.Normal, e.Modo);
    }

    [Fact]
    public void Aviso_novo_reinicia_o_tempo()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(5, e);
        e.Chegou(A2);
        Avancar(5, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Assert.Same(A2, e.Aviso);
    }

    [Fact]
    public void Mouse_em_cima_segura_o_aviso()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(1, e);
        e.MouseEntrou();
        Avancar(19, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        e.MouseSaiu();
        Avancar(5.9, e);
        Assert.Equal(ModoNotch.Aviso, e.Modo);
        Avancar(0.2, e);
        Assert.Equal(ModoNotch.Lembrete, e.Modo);
    }

    [Fact]
    public void Clique_no_lembrete_abre_o_calendario_e_limpa()
    {
        var e = Criar();
        e.Chegou(A1);
        Avancar(7, e);
        Assert.Equal(AcaoNotch.AbrirCalendario, e.Clicou());
        Assert.Equal(ModoNotch.Normal, e.Modo);
        Assert.Equal(AcaoNotch.AbrirCalendario, e.Clicou());
    }

    [Fact]
    public void Mudou_dispara_em_cada_troca_de_modo()
    {
        var e = Criar();
        var modos = new List<ModoNotch>();
        e.Mudou += () => modos.Add(e.Modo);
        e.Chegou(A1);
        Avancar(7, e);
        e.Clicou();
        Assert.Equal([ModoNotch.Aviso, ModoNotch.Lembrete, ModoNotch.Normal], modos);
    }
}

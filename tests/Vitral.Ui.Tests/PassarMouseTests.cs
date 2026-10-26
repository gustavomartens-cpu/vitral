using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PassarMouseTests
{
    private DateTime _agora = new(2026, 10, 26, 9, 0, 0);
    private PassarMouse Criar() => new(() => _agora);
    private AcaoPassar Avancar(double segundos, PassarMouse p) { _agora = _agora.AddSeconds(segundos); return p.Tique(); }

    [Fact]
    public void Abre_com_o_mouse_parado_300ms_na_ilha()
    {
        var p = Criar();
        p.Entrou(painel: false);
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(0.29, p));
        Assert.Equal(AcaoPassar.Abrir, Avancar(0.02, p));
        Assert.True(p.Aberto);
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(1, p));
    }

    [Fact]
    public void Ocioso_so_com_o_mouse_fora_e_o_painel_fechado()
    {
        var p = Criar();
        Assert.True(p.Ocioso);
        p.Entrou(painel: false);
        Assert.False(p.Ocioso);
        Avancar(0.3, p);
        p.Saiu(painel: false);
        Assert.False(p.Ocioso); // aberto: ainda vai fechar
        Avancar(0.4, p);
        Assert.True(p.Ocioso);
    }

    [Fact]
    public void Passar_rapido_pela_ilha_nao_abre()
    {
        var p = Criar();
        p.Entrou(painel: false);
        Avancar(0.1, p);
        p.Saiu(painel: false);
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(1, p));
    }

    [Fact]
    public void Fecha_400ms_depois_de_sair_da_ilha_e_do_painel()
    {
        var p = Criar();
        p.Entrou(painel: false);
        Avancar(0.3, p);
        p.Saiu(painel: false);
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(0.39, p));
        Assert.Equal(AcaoPassar.Fechar, Avancar(0.02, p));
        Assert.False(p.Aberto);
    }

    [Fact]
    public void Descer_da_ilha_ao_painel_nao_fecha()
    {
        var p = Criar();
        p.Entrou(painel: false);
        Avancar(0.3, p);
        p.Saiu(painel: false);
        Avancar(0.2, p);
        p.Entrou(painel: true);
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(2, p));
        p.Saiu(painel: true);
        Assert.Equal(AcaoPassar.Fechar, Avancar(0.4, p));
    }

    [Fact]
    public void Voltar_a_ilha_antes_da_folga_nao_fecha()
    {
        var p = Criar();
        p.Entrou(painel: false);
        Avancar(0.3, p);
        p.Saiu(painel: false);
        Avancar(0.2, p);
        p.Entrou(painel: false);
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(2, p));
    }

    [Fact]
    public void Fixado_pelo_clique_nao_fecha_ao_sair()
    {
        var p = Criar();
        p.Entrou(painel: false);
        Avancar(0.3, p);
        p.Fixou();
        p.Saiu(painel: false);
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(2, p));
    }

    [Fact]
    public void Fechado_por_outro_caminho_so_reabre_ao_entrar_de_novo()
    {
        var p = Criar();
        p.Entrou(painel: false);
        Avancar(0.3, p);
        p.Fechou(); // Esc, ou o painel foi trocado
        Assert.Equal(AcaoPassar.Nenhuma, Avancar(2, p));
        p.Saiu(painel: false);
        p.Entrou(painel: false);
        Assert.Equal(AcaoPassar.Abrir, Avancar(0.3, p));
    }
}

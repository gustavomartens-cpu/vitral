using Vitral.Core.Iniciar;

namespace Vitral.Core.Tests;

public sealed class AcompanhamentoIniciarTests
{
    private DateTime _agora = new(2026, 11, 1, 23, 0, 0);
    private readonly AcompanhamentoIniciar _a = new();

    private (bool Conectar, MotivoIniciar? Estado) Conferir(int? pid, string log = "", bool windhawk = false, ModoIniciar modo = ModoIniciar.Vitral) =>
        _a.Conferir(modo, pid, windhawk, log, _agora);

    [Fact]
    public void Voltar_a_Combinar_republica_o_estado_do_mesmo_Iniciar()
    {
        Assert.True(Conferir(10).Conectar);
        _a.Resultado(10, entrou: true, _agora);
        Assert.Equal((false, MotivoIniciar.Nenhum), Conferir(10));
        Assert.Equal((false, (MotivoIniciar?)null), Conferir(10, modo: ModoIniciar.Windows));
        Assert.Equal((false, MotivoIniciar.Nenhum), Conferir(10)); // voltou: o estado volta junto
    }

    [Fact]
    public void Resultado_de_outro_Iniciar_e_ignorado()
    {
        Conferir(10);
        Conferir(11);
        _a.Resultado(10, entrou: true, _agora);
        Assert.NotEqual(MotivoIniciar.Nenhum, Conferir(11).Estado);
    }

    [Fact]
    public void Recusa_tenta_de_novo_com_espera_crescente()
    {
        Conferir(10);
        _a.Resultado(10, entrou: false, _agora);
        Assert.Equal((false, MotivoIniciar.Recusou), Conferir(10));
        _agora = _agora.AddMinutes(1);
        Assert.True(Conferir(10).Conectar);
        _a.Resultado(10, entrou: false, _agora);
        _agora = _agora.AddMinutes(4);
        Assert.False(Conferir(10).Conectar);
        _agora = _agora.AddMinutes(1);
        Assert.True(Conferir(10).Conectar);
    }

    [Fact]
    public void ADVISE_que_chega_depois_da_recusa_vira_conectado()
    {
        Conferir(10);
        _a.Resultado(10, entrou: false, _agora);
        Assert.Equal(MotivoIniciar.Nenhum, Conferir(10, log: "23:00:09 ADVISE|hr=0\n").Estado);
    }

    [Fact]
    public void SEM_FUNDO_vale_ate_um_PINTADO_depois_dele()
    {
        Conferir(10);
        _a.Resultado(10, entrou: true, _agora);
        Assert.Equal(MotivoIniciar.WindowsMudou, Conferir(10, log: "MOLDURA\nSEM_FUNDO\n").Estado);
        Assert.Equal(MotivoIniciar.WindowsMudou, Conferir(10).Estado);
        Assert.Equal(MotivoIniciar.Nenhum, Conferir(10, log: "PINTADO|1\n").Estado);
        Assert.Equal(MotivoIniciar.Nenhum, Conferir(10, log: "SEM_FUNDO\nPINTADO|1\n").Estado);
    }

    [Fact]
    public void Windhawk_bloqueia_um_Iniciar_novo()
    {
        Assert.Equal((false, MotivoIniciar.Windhawk), Conferir(10, windhawk: true));
        Assert.True(Conferir(10).Conectar); // fechou o Windhawk
    }

    [Fact]
    public void Sem_Iniciar_nada_a_fazer() => Assert.False(Conferir(null).Conectar);
}

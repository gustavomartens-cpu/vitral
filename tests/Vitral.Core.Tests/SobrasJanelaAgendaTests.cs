using Vitral.Core.Agenda;
using Vitral.Core.Atualizacao;
using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

public sealed class SobrasJanelaAgendaTests
{
    [Fact]
    public void Reiniciar_para_atualizar_pergunta_antes_e_Cancelar_nao_reinicia()
    {
        var fonte = new FonteFalsa();
        bool deixa = false;
        var s = new ServicoAtualizacao(fonte, () => DateTime.Now) { PodeReiniciar = () => deixa };
        s.Reiniciar();
        Assert.Equal(0, fonte.Reinicios);
        deixa = true;
        s.Reiniciar();
        Assert.Equal(1, fonte.Reinicios);
    }

    [Fact]
    public void Login_que_passou_dos_5_minutos_explica_e_o_Cancelar_fica_quieto()
    {
        Assert.Null(RegrasAgenda.LoginInterrompido(usuarioCancelou: true, tempoEsgotou: true));
        Assert.Contains("5 minutos", RegrasAgenda.LoginInterrompido(usuarioCancelou: false, tempoEsgotou: true));
        Assert.Equal(RegrasAgenda.ErroDoGoogle(null, null), RegrasAgenda.LoginInterrompido(usuarioCancelou: false, tempoEsgotou: false));
    }

    private static readonly Area Claude = new(0, 0, 1000, 800);

    [Fact]
    public void Janela_pequena_por_cima_nao_esconde_o_Claude()
    {
        Assert.True(Cobertura.AVista(Claude, [new Area(100, 100, 300, 200)]));
    }

    [Fact]
    public void Janela_grande_por_cima_esconde_o_Claude_mesmo_vinda_de_outro_monitor()
    {
        Assert.False(Cobertura.AVista(Claude, [new Area(-200, 0, 1000, 800)]));
    }

    [Fact]
    public void Janelas_que_se_sobrepoem_nao_contam_duas_vezes()
    {
        // Duas janelas iguais cobrindo 40% cada: juntas cobrem 40%, não 80%.
        var meia = new Area(0, 0, 400, 800);
        Assert.True(Cobertura.AVista(Claude, [meia, meia]));
        Assert.False(Cobertura.AVista(Claude, [meia, new Area(400, 0, 300, 800)]));
    }

    [Fact]
    public void Sem_nada_por_cima_esta_a_vista()
    {
        Assert.True(Cobertura.AVista(Claude, []));
        Assert.False(Cobertura.AVista(new Area(0, 0, 0, 0), []));
    }
}

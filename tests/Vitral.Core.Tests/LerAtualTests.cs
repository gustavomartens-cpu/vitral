using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Tests;

public class LerAtualTests
{
    [Fact]
    public void Ler_atual_e_o_inverso_de_Calcular()
    {
        var original = new AjustesNativosPerfil(Modo.Escuro, Modo.Claro, "#21375E", false, true, true, false, Alinhamento.Esquerda);
        var registro = new RegistroEmMemoria();
        foreach (var v in AjustesNativos.Calcular(original)) registro.Gravar(v);

        Assert.Equal(original, AjustesNativos.LerAtual(registro));
    }

    [Fact]
    public void Valores_ausentes_viram_nulo()
    {
        Assert.Equal(new AjustesNativosPerfil(null, null, null, null, null, null, null, null),
            AjustesNativos.LerAtual(new RegistroEmMemoria()));
    }

    [Fact]
    public void Com_cor_automatica_ligada_nao_le_cor_fixa()
    {
        var registro = new RegistroEmMemoria();
        registro.Gravar(new ValorRegistro(@"Control Panel\Desktop", "AutoColorization", TipoValor.Dword, 1u));
        registro.Gravar(new ValorRegistro(@"Software\Microsoft\Windows\DWM", "AccentColor", TipoValor.Dword, 0xFF110A06u));
        var a = AjustesNativos.LerAtual(registro);
        Assert.True(a.CorDestaqueAutomatica);
        Assert.Null(a.CorDestaque);
    }
}

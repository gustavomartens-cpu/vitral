using Vitral.Core.Registro;

namespace Vitral.Core.Tests;

public class RegistroEmMemoriaTests
{
    private const string Chave = @"Software\Microsoft\Windows\DWM";

    [Fact]
    public void Ler_valor_inexistente_devolve_ausente()
    {
        var r = new RegistroEmMemoria();
        Assert.Null(r.Ler(Chave, "AccentColor", TipoValor.Dword).Dado);
    }

    [Fact]
    public void Gravar_e_ler_devolve_o_mesmo_valor()
    {
        var r = new RegistroEmMemoria();
        r.Gravar(new ValorRegistro(Chave, "AccentColor", TipoValor.Dword, 0xFF110A06u));
        var lido = r.Ler(Chave, "AccentColor", TipoValor.Dword);
        Assert.Equal(0xFF110A06u, lido.Dado);
        Assert.Equal(TipoValor.Dword, lido.Tipo);
    }

    [Fact]
    public void Gravar_em_valor_bloqueado_lanca_EscritaBloqueada()
    {
        var r = new RegistroEmMemoria();
        r.Bloqueados.Add((Chave, "AccentColor"));
        var v = new ValorRegistro(Chave, "AccentColor", TipoValor.Dword, 1u);
        var ex = Assert.Throws<EscritaBloqueadaException>(() => r.Gravar(v));
        Assert.Equal(v, ex.Valor);
    }

    [Fact]
    public void Apagar_faz_a_proxima_leitura_ser_ausente()
    {
        var r = new RegistroEmMemoria();
        r.Gravar(new ValorRegistro(Chave, "ColorPrevalence", TipoValor.Dword, 1u));
        r.Apagar(Chave, "ColorPrevalence");
        Assert.Null(r.Ler(Chave, "ColorPrevalence", TipoValor.Dword).Dado);
    }

    [Fact]
    public void Chaves_e_nomes_nao_diferenciam_maiusculas()
    {
        var r = new RegistroEmMemoria();
        r.Gravar(new ValorRegistro(Chave, "AccentColor", TipoValor.Dword, 7u));
        Assert.Equal(7u, r.Ler(Chave.ToUpperInvariant(), "accentcolor", TipoValor.Dword).Dado);
    }
}

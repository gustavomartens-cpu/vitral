using Vitral.Core.Doca;

namespace Vitral.Core.Tests;

public sealed class PacoteDaLojaTests
{
    private const string Velho = @"C:\Program Files\WindowsApps\Claude_4.29.0.0_x64__pzs8sxrjxfjjc\app\claude.exe";
    private const string Novo = @"C:\Program Files\WindowsApps\Claude_4.4212.0.0_x64__pzs8sxrjxfjjc\app\claude.exe";
    private const string PastaNova = @"C:\Program Files\WindowsApps\Claude_4.4212.0.0_x64__pzs8sxrjxfjjc";

    [Fact]
    public void Le_a_familia_e_o_resto_do_caminho()
    {
        var p = PacoteDaLoja.Ler(Velho);
        Assert.NotNull(p);
        Assert.Equal("Claude_pzs8sxrjxfjjc", p.Value.Familia);
        Assert.Equal(@"app\claude.exe", p.Value.Resto);
    }

    [Fact]
    public void Caminho_fora_da_loja_nao_e_pacote() => Assert.Null(PacoteDaLoja.Ler(@"C:\Program Files\Google\Chrome\Application\chrome.exe"));

    [Fact]
    public void Versoes_diferentes_do_mesmo_app_sao_o_mesmo_app() =>
        Assert.Equal(PacoteDaLoja.SemVersao(Velho), PacoteDaLoja.SemVersao(Novo.ToUpperInvariant()));

    [Fact]
    public void Fixado_da_versao_apagada_passa_para_a_pasta_atual()
    {
        var lista = PacoteDaLoja.Atualizar([new Fixado(Velho, null, "Claude", Velho)], c => c == Novo, _ => PastaNova);
        Assert.Equal(new Fixado(Novo, null, "Claude", Novo), Assert.Single(lista));
    }

    [Fact]
    public void Fixado_repetido_depois_da_atualizacao_fica_so_o_primeiro()
    {
        var lista = PacoteDaLoja.Atualizar(
            [new Fixado(Velho, null, "Claude", Velho), new Fixado(@"C:\o.lnk", "O", "Obsidian", null), new Fixado(Novo, null, "Claude", Novo)],
            c => c == Novo, _ => PastaNova);
        Assert.Equal(["Claude", "Obsidian"], lista.Select(f => f.Nome));
        Assert.Equal(Novo, lista[0].Caminho);
    }

    [Fact]
    public void Pacote_desinstalado_fica_como_esta()
    {
        var f = new Fixado(Velho, null, "Claude", Velho);
        Assert.Same(f, Assert.Single(PacoteDaLoja.Atualizar([f], _ => false, _ => null)));
    }
}

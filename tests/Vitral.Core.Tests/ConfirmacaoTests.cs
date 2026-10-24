using System.Diagnostics;
using Vitral.Cli;

namespace Vitral.Core.Tests;

public class ConfirmacaoTests
{
    [Fact]
    public void Entrada_encerrada_espera_o_prazo_e_nao_confirma()
    {
        var relogio = Stopwatch.StartNew();
        bool manter = Confirmacao.Esperar(() => null, TimeSpan.FromMilliseconds(400));
        Assert.False(manter);
        Assert.True(relogio.ElapsedMilliseconds >= 380, $"voltou em {relogio.ElapsedMilliseconds} ms, antes do prazo");
    }

    [Fact]
    public void Resposta_s_confirma_na_hora()
    {
        var relogio = Stopwatch.StartNew();
        Assert.True(Confirmacao.Esperar(() => " S ", TimeSpan.FromSeconds(10)));
        Assert.True(relogio.ElapsedMilliseconds < 2000);
    }

    [Fact]
    public void Outra_resposta_nao_confirma()
    {
        Assert.False(Confirmacao.Esperar(() => "n", TimeSpan.FromSeconds(10)));
    }
}

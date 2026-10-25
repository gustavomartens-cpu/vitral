using Vitral.Core.Aplicacao;
using Vitral.Core.Configuracao;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Servicos;

/// O que aconteceu ao abrir o app.
public sealed record EstadoInicial(bool TesteCancelado, string? Aviso);

/// Fluxos que a janela e a bandeja usam. O "Testar" fica marcado no config.json até ser confirmado
/// ou cancelado, para que um app morto no meio volte ao anterior quando abrir de novo.
public sealed class ServicoVitral(Aplicador aplicador, ServicoRetratos retratos, RepositorioPerfis perfis, ServicoConfig config)
{
    public Resultado Aplicar(string perfil)
    {
        var resultado = aplicador.Aplicar(perfis.Carregar(perfil));
        config.Gravar(config.Ler() with { PerfilAtivo = perfil, TesteEmAndamento = null });
        return resultado;
    }

    public Resultado IniciarTeste(string perfil)
    {
        var resultado = aplicador.Aplicar(perfis.Carregar(perfil));
        config.Gravar(config.Ler() with { TesteEmAndamento = new TesteEmAndamento(perfil, resultado.ArquivoRetratoAnterior!) });
        return resultado;
    }

    public void ConfirmarTeste()
    {
        var c = config.Ler();
        if (c.TesteEmAndamento is null) return;
        config.Gravar(c with { PerfilAtivo = c.TesteEmAndamento.Perfil, TesteEmAndamento = null });
    }

    /// Volta ao retrato de antes do teste. Lança se o retrato não existir mais (nunca finge que voltou).
    public IReadOnlyList<(ValorRegistro Valor, string Motivo)> CancelarTeste()
    {
        var c = config.Ler();
        if (c.TesteEmAndamento is null) return [];
        var retrato = retratos.Carregar(c.TesteEmAndamento.Retrato)
            ?? throw new InvalidOperationException(
                $"O retrato de antes do teste ({c.TesteEmAndamento.Retrato}) não existe mais. Use \"Desfazer\" ou \"Voltar ao Windows original\".");
        var naoVoltou = aplicador.Reverter(retrato);
        config.Gravar(c with { TesteEmAndamento = null });
        return naoVoltou;
    }

    public IReadOnlyList<(ValorRegistro Valor, string Motivo)> Desfazer()
    {
        var ultimo = retratos.Ultimo();
        return ultimo is null ? [] : aplicador.Restaurar(ultimo, "(antes de desfazer)");
    }

    public IReadOnlyList<(ValorRegistro Valor, string Motivo)> Original() =>
        aplicador.Restaurar(retratos.GarantirOriginal(), "(antes de original)");

    public EstadoInicial AoIniciar(bool inicializacaoDoWindows)
    {
        var c = config.Ler();
        if (c.TesteEmAndamento is not null)
        {
            try
            {
                CancelarTeste();
                return new EstadoInicial(true, null);
            }
            catch (InvalidOperationException e)
            {
                config.Gravar(c with { TesteEmAndamento = null });
                return new EstadoInicial(false, e.Message);
            }
        }

        if (inicializacaoDoWindows && c.PerfilAtivo is { } ativo)
        {
            try
            {
                aplicador.Aplicar(perfis.Carregar(ativo));
            }
            catch (Exception e) when (e is FileNotFoundException or PerfilInvalidoException)
            {
                return new EstadoInicial(false, $"O perfil ativo \"{ativo}\" não foi encontrado; nada foi aplicado.");
            }
        }
        return new EstadoInicial(false, null);
    }
}

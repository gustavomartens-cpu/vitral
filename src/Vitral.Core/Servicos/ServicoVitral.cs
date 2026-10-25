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
    public string? PerfilAtivo => config.Ler().PerfilAtivo;

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

    /// Nunca lança: qualquer falha vira aviso, para o app abrir sempre (inclusive no login).
    public EstadoInicial AoIniciar(bool inicializacaoDoWindows)
    {
        var c = config.Ler();
        if (c.TesteEmAndamento is not null)
        {
            try
            {
                var naoVoltou = CancelarTeste();
                return new EstadoInicial(true, naoVoltou.Count == 0 ? null : FraseParcial("restaurar", naoVoltou));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Retrato sumido ou ilegível: limpa a marca, senão o app tentaria (e falharia) a cada abertura.
                config.Gravar(c with { TesteEmAndamento = null });
                return new EstadoInicial(false, $"Um teste ficou aberto, mas não foi possível voltar ao anterior: {e.Message}");
            }
        }

        if (inicializacaoDoWindows && c.PerfilAtivo is { } ativo)
        {
            try
            {
                var resultado = aplicador.Aplicar(perfis.Carregar(ativo));
                if (!resultado.Ok) return new EstadoInicial(false, $"\"{ativo}\" aplicado em parte: {FraseParcial("aplicar", resultado.NaoAplicados)}");
            }
            catch (FileNotFoundException)
            {
                return new EstadoInicial(false, $"O perfil ativo \"{ativo}\" não foi encontrado; nada foi aplicado.");
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return new EstadoInicial(false, $"O perfil ativo \"{ativo}\" não pôde ser aplicado: {e.Message}");
            }
        }
        return new EstadoInicial(false, null);
    }

    private static string FraseParcial(string verbo, IReadOnlyList<(ValorRegistro Valor, string Motivo)> itens) =>
        $"o Windows não deixou {verbo} {string.Join(", ", itens.Select(i => i.Valor.Nome))}.";
}

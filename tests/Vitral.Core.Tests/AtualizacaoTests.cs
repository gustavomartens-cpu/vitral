using System.Net;
using Vitral.Core.Atualizacao;

namespace Vitral.Core.Tests;

internal sealed class FonteFalsa : IFonteAtualizacao
{
    public bool Ligada { get; set; } = true;
    public string? Nova { get; set; }
    public Exception? Erro { get; set; }
    public int Procuras { get; private set; }
    public int Baixadas { get; private set; }
    public int Reinicios { get; private set; }

    public Task<string?> ProcurarAsync()
    {
        Procuras++;
        return Erro is not null ? Task.FromException<string?>(Erro) : Task.FromResult(Nova);
    }

    public Task BaixarAsync() { Baixadas++; return Task.CompletedTask; }
    public void Reiniciar() => Reinicios++;
}

public sealed class AtualizacaoTests
{
    private DateTime _agora = new(2026, 10, 26, 9, 0, 0);
    private readonly FonteFalsa _fonte = new();
    private ServicoAtualizacao Criar() => new(_fonte, () => _agora);
    private async Task Passar(ServicoAtualizacao s, TimeSpan t) { _agora += t; await s.TiqueAsync(); }

    [Fact]
    public async Task Nao_confere_antes_de_um_minuto()
    {
        var s = Criar();
        await Passar(s, TimeSpan.FromSeconds(59));
        Assert.Equal(0, _fonte.Procuras);
        await Passar(s, TimeSpan.FromSeconds(2));
        Assert.Equal(1, _fonte.Procuras);
    }

    [Fact]
    public async Task Confere_de_novo_em_seis_horas()
    {
        var s = Criar();
        await Passar(s, TimeSpan.FromMinutes(1));
        await Passar(s, TimeSpan.FromHours(6) - TimeSpan.FromMinutes(1));
        Assert.Equal(1, _fonte.Procuras);
        await Passar(s, TimeSpan.FromMinutes(2));
        Assert.Equal(2, _fonte.Procuras);
    }

    [Fact]
    public async Task Versao_nova_baixa_e_avisa_uma_vez()
    {
        _fonte.Nova = "0.7.3";
        var s = Criar();
        var estados = new List<EstadoAtualizacao>();
        var prontas = new List<string>();
        s.Mudou += () => estados.Add(s.Estado);
        s.Pronta += prontas.Add;
        await Passar(s, TimeSpan.FromMinutes(1));
        Assert.Contains(EstadoAtualizacao.Baixando, estados);
        Assert.Equal(EstadoAtualizacao.Pronta, s.Estado);
        Assert.Equal("0.7.3", s.Versao);
        Assert.Equal(["0.7.3"], prontas);
        Assert.Equal(1, _fonte.Baixadas);
        await Passar(s, TimeSpan.FromHours(7));
        Assert.Equal(1, _fonte.Procuras);
        Assert.Single(prontas);
    }

    [Fact]
    public async Task Mesma_versao_fica_atualizado()
    {
        var s = Criar();
        await Passar(s, TimeSpan.FromMinutes(1));
        Assert.Equal(EstadoAtualizacao.Atualizado, s.Estado);
        Assert.Equal(0, _fonte.Baixadas);
    }

    [Fact]
    public async Task Falha_comum_vai_a_Falhou_e_tenta_de_novo()
    {
        _fonte.Erro = new HttpRequestException("sem rede");
        var s = Criar();
        var prontas = 0;
        s.Pronta += _ => prontas++;
        await Passar(s, TimeSpan.FromMinutes(1));
        Assert.Equal(EstadoAtualizacao.Falhou, s.Estado);
        Assert.Equal(0, prontas);
        await Passar(s, TimeSpan.FromHours(6));
        Assert.Equal(2, _fonte.Procuras);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Http_401_ou_403_vai_a_AcessoNegado(HttpStatusCode codigo)
    {
        _fonte.Erro = new InvalidOperationException("falhou", new HttpRequestException("negado", null, codigo));
        var s = Criar();
        await Passar(s, TimeSpan.FromMinutes(1));
        Assert.Equal(EstadoAtualizacao.AcessoNegado, s.Estado);
    }

    [Fact]
    public async Task Fonte_desligada_nunca_confere()
    {
        _fonte.Ligada = false;
        var s = Criar();
        Assert.Equal(EstadoAtualizacao.Desligada, s.Estado);
        await Passar(s, TimeSpan.FromHours(10));
        await s.ProcurarAgoraAsync();
        Assert.Equal(0, _fonte.Procuras);
    }

    [Fact]
    public async Task Procurar_agora_confere_fora_do_ciclo()
    {
        var s = Criar();
        await s.ProcurarAgoraAsync();
        Assert.Equal(1, _fonte.Procuras);
    }

    [Theory]
    [InlineData(EstadoAtualizacao.Desligada, null, "Atualização desligada (versão de desenvolvimento)")]
    [InlineData(EstadoAtualizacao.Atualizado, null, "Atualizado")]
    [InlineData(EstadoAtualizacao.Baixando, "0.7.3", "Baixando a versão 0.7.3…")]
    [InlineData(EstadoAtualizacao.Pronta, "0.7.3", "Versão 0.7.3 pronta — reiniciar")]
    [InlineData(EstadoAtualizacao.Falhou, null, "Não foi possível procurar atualizações")]
    [InlineData(EstadoAtualizacao.AcessoNegado, null, "Não foi possível procurar atualizações (acesso negado: o token venceu?)")]
    public void Textos_da_linha_de_estado(EstadoAtualizacao estado, string? versao, string texto) =>
        Assert.Equal(texto, TextoAtualizacao.Para(estado, versao));
}

public sealed class TokenEmbutidoTests
{
    [Fact]
    public void TokenEmbutido_ausente_e_null() => Assert.Null(TokenEmbutido.Ler(typeof(TokenEmbutidoTests).Assembly));

    [Fact]
    public void TokenEmbutido_presente() => Assert.Equal("abc", TokenEmbutido.Ler(typeof(TokenEmbutidoTests).Assembly, "TokenTeste"));

    [Fact]
    public void TokenEmbutido_vazio_e_null() => Assert.Null(TokenEmbutido.Ler(typeof(TokenEmbutidoTests).Assembly, "TokenVazio"));
}

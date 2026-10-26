using System.Text.Json.Nodes;
using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

public sealed class GanchosClaudeTests
{
    private const string Exe = @"C:\Users\g\AppData\Local\VitralApp\current\Vitral.exe";
    private static readonly string Comando = GanchosClaude.Comando(Exe);

    private static JsonNode Raiz(string texto) => JsonNode.Parse(texto)!;

    private static int Nossos(string texto, string evento) =>
        Raiz(texto)["hooks"]?[evento]?.AsArray().Count(i => i!["hooks"]!.AsArray().Any(h => ((string?)h!["command"])?.Contains(GanchosClaude.Marca) == true)) ?? 0;

    [Fact]
    public void Ligar_em_arquivo_vazio_cria_os_dois_ganchos()
    {
        var t = GanchosClaude.Ligar("", Comando);
        Assert.True(GanchosClaude.EstaLigado(t));
        Assert.Equal(1, Nossos(t, "Stop"));
        Assert.Equal(1, Nossos(t, "Notification"));
        Assert.Equal(Comando, (string?)Raiz(t)["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]);
    }

    [Fact]
    public void Ligar_preserva_outras_chaves_e_ganchos()
    {
        const string original = """
            {
              // comentário do usuário
              "model": "x",
              "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "echo oi" } ] } ] }
            }
            """;
        var t = GanchosClaude.Ligar(original, Comando);
        Assert.Equal("x", (string?)Raiz(t)["model"]);
        var stop = Raiz(t)["hooks"]!["Stop"]!.AsArray();
        Assert.Equal(2, stop.Count);
        Assert.Equal("echo oi", (string?)stop[0]!["hooks"]![0]!["command"]);
    }

    [Fact]
    public void Ligar_duas_vezes_nao_duplica()
    {
        var t = GanchosClaude.Ligar(GanchosClaude.Ligar("{}", Comando), Comando);
        Assert.Equal(1, Nossos(t, "Stop"));
        Assert.Equal(1, Nossos(t, "Notification"));
    }

    [Fact]
    public void Ligar_com_caminho_novo_troca_o_antigo()
    {
        var novo = GanchosClaude.Comando(@"G:\dev\vitral\bin\Vitral.exe");
        var t = GanchosClaude.Ligar(GanchosClaude.Ligar("{}", Comando), novo);
        Assert.Equal(1, Nossos(t, "Stop"));
        Assert.Equal(novo, (string?)Raiz(t)["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]);
    }

    [Fact]
    public void Desligar_tira_so_os_nossos_e_limpa_vazios()
    {
        const string alheio = """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo oi"}]}]}}""";
        var comAlheio = GanchosClaude.Desligar(GanchosClaude.Ligar(alheio, Comando));
        Assert.False(GanchosClaude.EstaLigado(comAlheio));
        Assert.Single(Raiz(comAlheio)["hooks"]!["Stop"]!.AsArray());
        Assert.Null(Raiz(comAlheio)["hooks"]!["Notification"]);

        var soNossos = GanchosClaude.Desligar(GanchosClaude.Ligar("""{"model":"x"}""", Comando));
        Assert.Null(Raiz(soNossos)["hooks"]);
        Assert.Equal("x", (string?)Raiz(soNossos)["model"]);
    }

    [Fact]
    public void Desligar_quando_desligado_nao_muda_nada()
    {
        const string original = """{"model":"x","hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo oi"}]}]}}""";
        Assert.True(JsonNode.DeepEquals(Raiz(original), Raiz(GanchosClaude.Desligar(original))));
    }

    [Fact]
    public void Json_com_defeito_lanca_FormatException()
    {
        Assert.Throws<FormatException>(() => GanchosClaude.Ligar("{\"hooks\": [", Comando));
        Assert.Throws<FormatException>(() => GanchosClaude.Desligar("{\"hooks\": ["));
        Assert.Throws<FormatException>(() => GanchosClaude.Ligar("[1,2]", Comando));
    }

    [Theory]
    [InlineData("""{"hooks":[]}""")]
    [InlineData("""{"hooks":"x"}""")]
    [InlineData("""{"permissions":{},"permissions":{}}""")]
    public void Formato_inesperado_nao_lanca_no_EstaLigado_e_vira_FormatException_ao_gravar(string texto)
    {
        Assert.False(GanchosClaude.EstaLigado(texto));
        Assert.Throws<FormatException>(() => GanchosClaude.Ligar(texto, Comando));
        Assert.Throws<FormatException>(() => GanchosClaude.Desligar(texto));
    }

    [Fact]
    public void Item_estranho_na_lista_e_preservado()
    {
        var t = GanchosClaude.Ligar("""{"hooks":{"Stop":["x"]}}""", Comando);
        Assert.True(GanchosClaude.EstaLigado(t));
        Assert.Equal("x", (string?)Raiz(t)["hooks"]!["Stop"]![0]);
        Assert.Equal("x", (string?)Raiz(GanchosClaude.Desligar(t))["hooks"]!["Stop"]![0]);
    }

    [Fact]
    public void Ligar_escreve_os_tres_eventos_com_timeout_na_permissao()
    {
        var t = GanchosClaude.Ligar("", Comando);
        Assert.Equal(1, Nossos(t, "Stop"));
        Assert.Equal(1, Nossos(t, "Notification"));
        Assert.Equal(1, Nossos(t, "PermissionRequest"));
        Assert.Equal(75, (int)Raiz(t)["hooks"]!["PermissionRequest"]![0]!["hooks"]![0]!["timeout"]!);
    }

    [Fact]
    public void So_os_dois_antigos_conta_como_desligado_e_ligar_completa()
    {
        var item = "{\"hooks\":[{\"type\":\"command\",\"command\":" + System.Text.Json.JsonSerializer.Serialize(Comando) + "}]}";
        var antigo = "{\"hooks\":{\"Stop\":[" + item + "],\"Notification\":[" + item + "]}}";
        Assert.False(GanchosClaude.EstaLigado(antigo));
        var t = GanchosClaude.Ligar(antigo, Comando);
        Assert.True(GanchosClaude.EstaLigado(t));
        Assert.Equal(1, Nossos(t, "Stop"));
        Assert.Equal(1, Nossos(t, "Notification"));
        Assert.Equal(1, Nossos(t, "PermissionRequest"));
    }

    [Fact]
    public void Desligar_tira_os_tres() => Assert.Null(Raiz(GanchosClaude.Desligar(GanchosClaude.Ligar("{}", Comando)))["hooks"]);
}

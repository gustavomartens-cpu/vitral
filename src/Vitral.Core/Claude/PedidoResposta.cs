using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vitral.Core.Claude;

/// A sessão terminou fora do app e espera uma resposta pelo notch (spec da resposta §3.4): vem do gancho Stop e viaja
/// pelo canal como uma linha JSON; o texto digitado volta na mesma conexão.
public sealed record PedidoResposta(string Id, string Sessao, string Completo, DateTime Hora)
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// O id da sessão do Claude: o aviso seguinte dela descarta a ficha.
    public string? SessaoId { get; init; }

    /// A ficha para o aviso de fim de tarefa (a mensagem inteira; sem ela, o resumo).
    public static PedidoResposta? DeAviso(AvisoClaude aviso, DateTime hora) =>
        aviso.Tipo != TipoAviso.Terminou ? null
            : new PedidoResposta(Guid.NewGuid().ToString("N"), aviso.Sessao, aviso.Completo ?? aviso.Texto, hora) { SessaoId = aviso.SessaoId };

    public string ParaLinha() => JsonSerializer.Serialize(new Linha("Resposta", Id, Sessao, Completo, Hora, SessaoId), Opcoes);

    public static bool EhPedido(string? linha) => Ler(linha)?.Tipo == "Resposta";

    public static PedidoResposta? DeLinha(string? linha) =>
        Ler(linha) is { Tipo: "Resposta", Id: { } id, Sessao: { } s, Completo: { } c, Hora: { } h } l
            ? new PedidoResposta(id, s, c, h) { SessaoId = l.SessaoId } : null;

    private static Linha? Ler(string? linha)
    {
        if (string.IsNullOrWhiteSpace(linha) || linha.Length > AvisoClaude.MaximoLinha) return null;
        try { return JsonSerializer.Deserialize<Linha>(linha, Opcoes); }
        catch (JsonException) { return null; }
    }

    private sealed record Linha(string? Tipo, string? Id, string? Sessao, string? Completo, DateTime? Hora, string? SessaoId);
}

/// O texto digitado no notch: a linha que volta pelo canal e o que o mensageiro devolve ao Claude no Stop.
public static class RespostaTexto
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Linha(string id, string texto) => JsonSerializer.Serialize(new Resposta(id, texto), Opcoes);

    /// O texto, se a linha é do pedido esperado e não está em branco.
    public static string? DaLinha(string? linha, string idEsperado)
    {
        if (string.IsNullOrWhiteSpace(linha) || linha.Length > AvisoClaude.MaximoLinha) return null;
        try
        {
            var r = JsonSerializer.Deserialize<Resposta>(linha, Opcoes);
            return r is not null && r.Id == idEsperado && !string.IsNullOrWhiteSpace(r.Texto) ? r.Texto : null;
        }
        catch (JsonException) { return null; }
    }

    /// O Claude continua com o texto como instrução (gancho Stop, "block").
    public static string ParaClaude(string texto) =>
        new JsonObject { ["decision"] = "block", ["reason"] = texto }.ToJsonString();

    private sealed record Resposta(string? Id, string? Texto);
}

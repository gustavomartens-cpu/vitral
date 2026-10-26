using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vitral.Core.Claude;

public enum EscolhaPermissao { Permitir, SempreNaSessao, Negar }

/// Pedido de permissão do Claude Code para o notch (spec das permissões §3.2): vem do gancho PermissionRequest e viaja
/// pelo canal como uma linha JSON; a resposta volta na mesma conexão.
public sealed record PedidoPermissao(string Id, string Sessao, string Ferramenta, string Titulo, string Resumo)
{
    private const int MaximoResumo = 200;
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static PedidoPermissao? DeGancho(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || Campo(raiz, "hook_event_name") != "PermissionRequest") return null;
            var ferramenta = Campo(raiz, "tool_name") ?? "?";
            var entrada = raiz.TryGetProperty("tool_input", out var e) && e.ValueKind == JsonValueKind.Object ? e : default;
            string? Entrada(string nome) => entrada.ValueKind == JsonValueKind.Object ? Campo(entrada, nome) : null;
            string? Arquivo() => Entrada("file_path") is { } f ? Path.GetFileName(f.TrimEnd('\\', '/')) : null;

            var (titulo, resumo) = ferramenta switch
            {
                "Bash" => ("Claude quer rodar um comando", Entrada("command")),
                "Edit" or "MultiEdit" => ("Claude quer editar um arquivo", Arquivo()),
                "Write" => ("Claude quer criar um arquivo", Arquivo()),
                "WebFetch" => ("Claude quer abrir um endereço", Entrada("url")),
                _ when ferramenta.StartsWith("mcp__", StringComparison.Ordinal) =>
                    ("Claude quer usar uma ferramenta", ferramenta[(ferramenta.LastIndexOf("__", StringComparison.Ordinal) + 2)..]),
                _ => ("Claude quer usar uma ferramenta", ferramenta),
            };
            var pasta = Campo(raiz, "cwd")?.TrimEnd('\\', '/');
            var sessao = string.IsNullOrEmpty(pasta) ? "Claude" : Path.GetFileName(pasta);
            return new PedidoPermissao(Guid.NewGuid().ToString("N"), string.IsNullOrEmpty(sessao) ? "Claude" : sessao, ferramenta,
                titulo, AvisoClaude.Encurtar(string.IsNullOrWhiteSpace(resumo) ? ferramenta : resumo, MaximoResumo));
        }
        catch (JsonException) { return null; }
    }

    private static string? Campo(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public string ParaLinha() => JsonSerializer.Serialize(new Linha("Permissao", Id, Sessao, Ferramenta, Titulo, Resumo), Opcoes);

    public static bool EhPedido(string? linha) => Ler(linha)?.Tipo == "Permissao";

    public static PedidoPermissao? DeLinha(string? linha) =>
        Ler(linha) is { Tipo: "Permissao", Id: { } id, Sessao: { } s, Ferramenta: { } f, Titulo: { } t, Resumo: { } r }
            ? new PedidoPermissao(id, s, f, t, r) : null;

    private static Linha? Ler(string? linha)
    {
        if (string.IsNullOrWhiteSpace(linha) || linha.Length > AvisoClaude.MaximoLinha) return null;
        try { return JsonSerializer.Deserialize<Linha>(linha, Opcoes); }
        catch (JsonException) { return null; }
    }

    private sealed record Linha(string? Tipo, string? Id, string? Sessao, string? Ferramenta, string? Titulo, string? Resumo);
}

/// A resposta do notch: a linha que volta pelo canal e o JSON que o mensageiro devolve ao Claude (spec §3.3).
public static class RespostaPermissao
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Linha(string id, EscolhaPermissao escolha) => JsonSerializer.Serialize(new Resposta(id, escolha.ToString()), Opcoes);

    public static EscolhaPermissao? DaLinha(string? linha, string idEsperado)
    {
        if (string.IsNullOrWhiteSpace(linha) || linha.Length > AvisoClaude.MaximoLinha) return null;
        try
        {
            var r = JsonSerializer.Deserialize<Resposta>(linha, Opcoes);
            return r is not null && r.Id == idEsperado && Enum.TryParse<EscolhaPermissao>(r.EscolhaPermissao, out var e) ? e : null;
        }
        catch (JsonException) { return null; }
    }

    public static string ParaClaude(EscolhaPermissao escolha, string jsonDoGancho)
    {
        var decisao = new JsonObject();
        if (escolha == EscolhaPermissao.Negar)
        {
            decisao["behavior"] = "deny";
            decisao["message"] = "Negado no notch do Vitral.";
            decisao["interrupt"] = false;
        }
        else
        {
            decisao["behavior"] = "allow";
            if (escolha == EscolhaPermissao.SempreNaSessao)
                decisao["updatedPermissions"] = new JsonArray(new JsonObject
                {
                    ["type"] = "addRules",
                    ["rules"] = RegrasDaSessao(jsonDoGancho),
                    ["behavior"] = "allow",
                    ["destination"] = "session",
                });
        }
        return new JsonObject
        {
            ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PermissionRequest", ["decision"] = decisao },
        }.ToJsonString();
    }

    /// A regra sugerida pelo Claude; sem ela, o comando exato no Bash ou a ferramenta inteira nas outras.
    private static JsonArray RegrasDaSessao(string jsonDoGancho)
    {
        JsonNode? raiz = null;
        try { raiz = JsonNode.Parse(jsonDoGancho); } catch (JsonException) { }
        if (raiz?["permission_suggestions"] is JsonArray sugestoes)
            foreach (var s in sugestoes)
                if (s is JsonObject o && (string?)o["type"] == "addRules" && (string?)o["behavior"] == "allow" && o["rules"] is JsonArray regras)
                    return (JsonArray)regras.DeepClone();
        var ferramenta = (string?)raiz?["tool_name"] ?? "?";
        var regra = new JsonObject { ["toolName"] = ferramenta };
        // Ferramentas de comando (Bash, PowerShell): a regra vale só para esse comando, nunca para a ferramenta inteira.
        if (raiz?["tool_input"]?["command"] is JsonValue c && c.TryGetValue<string>(out var comando))
            regra["ruleContent"] = comando;
        return new JsonArray(regra);
    }

    private sealed record Resposta(string? Id, string? EscolhaPermissao);
}

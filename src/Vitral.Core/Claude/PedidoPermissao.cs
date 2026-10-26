using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Vitral.Core.Claude;

public enum EscolhaPermissao { Permitir, SempreNaSessao, Negar }

/// Pedido de permissão do Claude Code para o notch (spec das permissões §3.2): vem do gancho PermissionRequest e viaja
/// pelo canal como uma linha JSON; a resposta volta na mesma conexão.
public sealed partial record PedidoPermissao(string Id, string Sessao, string Ferramenta, string Titulo, string Resumo)
{
    private const int MaximoResumo = 200, MaximoCompleto = 1500;
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// O id da sessão do Claude (session_id): só um aviso dessa mesma sessão descarta o pedido.
    public string? SessaoId { get; init; }

    /// O que o Claude quer fazer, sem cortes (a dica do mouse na ficha).
    public string? Completo { get; init; }

    /// O que o "Sempre nesta sessão" libera, dito na dica do botão; null quando não há regra segura e o botão some.
    public string? AlcanceSempre { get; init; }

    public static PedidoPermissao? DeGancho(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JsonObject? raiz;
        try { raiz = JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
        if (raiz is null || Texto(raiz["hook_event_name"]) != "PermissionRequest") return null;

        var ferramenta = Texto(raiz["tool_name"]) ?? "?";
        var entrada = raiz["tool_input"] as JsonObject;
        var comando = Texto(entrada?["command"]);
        var caminho = Texto(entrada?["file_path"]);
        string? Arquivo() => caminho is null ? null : Path.GetFileName(caminho.TrimEnd('\\', '/'));

        // Qualquer ferramenta com comando (Bash, PowerShell e as de terminal) mostra o comando como ele é.
        var (titulo, resumo, completo) = ferramenta switch
        {
            _ when comando is not null => ("Claude quer rodar um comando", comando, comando),
            "Edit" or "MultiEdit" or "NotebookEdit" => ("Claude quer editar um arquivo", Arquivo(), caminho),
            "Write" => ("Claude quer criar um arquivo", Arquivo(), caminho),
            "WebFetch" => ("Claude quer abrir um endereço", Texto(entrada?["url"]), Texto(entrada?["url"])),
            _ => ("Claude quer usar uma ferramenta", NomeMostrado(ferramenta), ferramenta),
        };
        var pasta = Texto(raiz["cwd"])?.TrimEnd('\\', '/');
        var sessao = string.IsNullOrEmpty(pasta) ? "Claude" : Path.GetFileName(pasta);
        return new PedidoPermissao(Guid.NewGuid().ToString("N"), string.IsNullOrEmpty(sessao) ? "Claude" : sessao, ferramenta,
            titulo, Compactar(string.IsNullOrWhiteSpace(resumo) ? ferramenta : resumo, MaximoResumo))
        {
            SessaoId = Texto(raiz["session_id"]),
            Completo = completo is null ? null : completo.Length <= MaximoCompleto ? completo : completo[..(MaximoCompleto - 1)] + "…",
            AlcanceSempre = Sempre(ferramenta, entrada)?.Descricao,
        };
    }

    /// A regra exata do "Sempre nesta sessão" e como ela é descrita na ficha (spec das permissões §3.3). Nunca a
    /// sugestão do Claude, que costuma ser mais larga que o pedido (um prefixo como "git push:*"). Comando com "*" não
    /// tem regra: o Claude leria o "*" como curinga.
    internal static (JsonObject Regra, string Descricao)? Sempre(string ferramenta, JsonNode? entrada)
    {
        if (Texto(entrada?["command"]) is { } comando)
            return comando.Contains('*') ? null
                : (new JsonObject { ["toolName"] = ferramenta, ["ruleContent"] = comando }, "Só este comando, nesta sessão");
        if (ferramenta == "WebFetch")
            return Uri.TryCreate(Texto(entrada?["url"]), UriKind.Absolute, out var u) && u.Host.Length > 0
                ? (new JsonObject { ["toolName"] = ferramenta, ["ruleContent"] = "domain:" + u.Host }, $"Qualquer endereço em {u.Host}, nesta sessão")
                : null;
        var descricao = ferramenta switch
        {
            "Edit" or "MultiEdit" or "NotebookEdit" => "Qualquer edição de arquivo, nesta sessão",
            "Write" => "Qualquer criação de arquivo, nesta sessão",
            _ => $"A ferramenta {NomeMostrado(ferramenta)}, nesta sessão",
        };
        return (new JsonObject { ["toolName"] = ferramenta }, descricao);
    }

    /// "mcp__servidor__ferramenta" vira "servidor · ferramenta"; as outras ficam como vêm.
    private static string NomeMostrado(string ferramenta)
    {
        if (!ferramenta.StartsWith("mcp__", StringComparison.Ordinal)) return ferramenta;
        var resto = ferramenta[5..];
        var corte = resto.LastIndexOf("__", StringComparison.Ordinal);
        return corte <= 0 ? resto : $"{resto[..corte]} · {resto[(corte + 2)..]}";
    }

    /// Uma linha só, no máximo <paramref name="maximo"/> caracteres. Diferente do Encurtar dos avisos, não tira "*",
    /// "`" nem "#": num comando, eles mudam o que roda.
    private static string Compactar(string texto, int maximo)
    {
        var limpo = Espacos().Replace(texto, " ").Trim();
        return limpo.Length <= maximo ? limpo : limpo[..(maximo - 1)].TrimEnd() + "…";
    }

    [GeneratedRegex(@"\s+")] private static partial Regex Espacos();

    internal static string? Texto(JsonNode? no) => no is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// A linha do canal; o texto completo encolhe se a linha passaria do limite (texto com muito escape).
    public string ParaLinha()
    {
        var completo = Completo;
        while (true)
        {
            var linha = JsonSerializer.Serialize(new Linha("Permissao", Id, Sessao, Ferramenta, Titulo, Resumo, SessaoId, completo, AlcanceSempre), Opcoes);
            if (linha.Length <= AvisoClaude.MaximoLinha || completo is null) return linha;
            completo = completo.Length > 100 ? completo[..(completo.Length / 2)] + "…" : null;
        }
    }

    public static bool EhPedido(string? linha) => Ler(linha)?.Tipo == "Permissao";

    public static PedidoPermissao? DeLinha(string? linha) =>
        Ler(linha) is { Tipo: "Permissao", Id: { } id, Sessao: { } s, Ferramenta: { } f, Titulo: { } t, Resumo: { } r } l
            ? new PedidoPermissao(id, s, f, t, r) { SessaoId = l.SessaoId, Completo = l.Completo, AlcanceSempre = l.AlcanceSempre }
            : null;

    private static Linha? Ler(string? linha)
    {
        if (string.IsNullOrWhiteSpace(linha) || linha.Length > AvisoClaude.MaximoLinha) return null;
        try { return JsonSerializer.Deserialize<Linha>(linha, Opcoes); }
        catch (JsonException) { return null; }
    }

    private sealed record Linha(string? Tipo, string? Id, string? Sessao, string? Ferramenta, string? Titulo, string? Resumo,
        string? SessaoId, string? Completo, string? AlcanceSempre);
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
            return r is not null && r.Id == idEsperado && Enum.TryParse<EscolhaPermissao>(r.Escolha, out var e) ? e : null;
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
            // Sem regra segura (comando com "*"), o "Sempre" vale só desta vez.
            if (escolha == EscolhaPermissao.SempreNaSessao && RegraDaSessao(jsonDoGancho) is { } regra)
                decisao["updatedPermissions"] = new JsonArray(new JsonObject
                {
                    ["type"] = "addRules",
                    ["rules"] = new JsonArray(regra),
                    ["behavior"] = "allow",
                    ["destination"] = "session",
                });
        }
        return new JsonObject
        {
            ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PermissionRequest", ["decision"] = decisao },
        }.ToJsonString();
    }

    private static JsonObject? RegraDaSessao(string jsonDoGancho)
    {
        JsonNode? raiz = null;
        try { raiz = JsonNode.Parse(jsonDoGancho); } catch (JsonException) { }
        return PedidoPermissao.Sempre(PedidoPermissao.Texto(raiz?["tool_name"]) ?? "?", raiz?["tool_input"])?.Regra;
    }

    private sealed record Resposta(string? Id, string? Escolha);
}

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vitral.Core.Claude;

/// Os ganchos do Vitral no settings.json do Claude Code (spec do notch vivo §4.1 e §4.2): um item em hooks.Stop e outro em
/// hooks.Notification, os dois rodando "Vitral.exe" --aviso. Mexe só nos nossos; o resto do arquivo fica como estava
/// (comentários são aceitos na leitura, mas não voltam na gravação).
public static class GanchosClaude
{
    /// Um item é nosso quando algum comando dele tem este trecho.
    public const string Marca = "Vitral.exe\" --aviso";

    private static readonly string[] Eventos = ["Stop", "Notification"];

    private static readonly JsonDocumentOptions Leitura = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly JsonSerializerOptions Escrita = new() { WriteIndented = true };

    public static string Comando(string caminhoExe) => $"\"{caminhoExe}\" --aviso";

    public static bool EstaLigado(string texto)
    {
        try
        {
            var raiz = Ler(texto);
            return Eventos.All(e => Lista(raiz, e)?.Any(EhNosso) == true);
        }
        catch (FormatException) { return false; }
    }

    public static string Ligar(string texto, string comando)
    {
        var raiz = Ler(texto);
        if (raiz["hooks"] is not JsonObject hooks) raiz["hooks"] = hooks = [];
        foreach (var evento in Eventos)
        {
            if (hooks[evento] is not JsonArray lista) hooks[evento] = lista = [];
            Remover(lista);
            lista.Add(new JsonObject { ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = comando }) });
        }
        return raiz.ToJsonString(Escrita);
    }

    public static string Desligar(string texto)
    {
        var raiz = Ler(texto);
        if (raiz["hooks"] is not JsonObject hooks) return raiz.ToJsonString(Escrita);
        foreach (var evento in Eventos)
        {
            if (hooks[evento] is not JsonArray lista) continue;
            Remover(lista);
            if (lista.Count == 0) hooks.Remove(evento);
        }
        if (hooks.Count == 0) raiz.Remove("hooks");
        return raiz.ToJsonString(Escrita);
    }

    /// Lê e confere o formato que nos interessa; qualquer coisa fora dele (JSON quebrado, chave repetida, "hooks" ou a
    /// lista de um evento com outro tipo) vira FormatException, e nada é gravado.
    private static JsonObject Ler(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return [];
        try
        {
            if (JsonNode.Parse(texto, documentOptions: Leitura) is not JsonObject raiz)
                throw new FormatException("O settings.json do Claude não é um objeto JSON.");
            _ = raiz.Count; // o JsonObject só confere chaves repetidas quando é aberto
            if (raiz["hooks"] is { } hooks)
            {
                if (hooks is not JsonObject eventos) throw new FormatException("\"hooks\" no settings.json do Claude não é um objeto.");
                foreach (var evento in Eventos)
                    if (eventos[evento] is { } lista && lista is not JsonArray)
                        throw new FormatException($"\"hooks.{evento}\" no settings.json do Claude não é uma lista.");
            }
            return raiz;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new FormatException("O settings.json do Claude está com defeito.", e);
        }
    }

    private static JsonArray? Lista(JsonObject raiz, string evento) => raiz["hooks"]?[evento] as JsonArray;

    private static bool EhNosso(JsonNode? item) =>
        item is JsonObject o && o["hooks"] is JsonArray comandos
        && comandos.Any(c => c is JsonObject co && co["command"] is JsonValue v && v.TryGetValue<string>(out var s) && s.Contains(Marca, StringComparison.OrdinalIgnoreCase));

    private static void Remover(JsonArray lista)
    {
        for (int i = lista.Count - 1; i >= 0; i--)
            if (EhNosso(lista[i])) lista.RemoveAt(i);
    }
}

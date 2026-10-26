using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vitral.Core.Claude;

/// Atualizacao: aviso do próprio Vitral (versão nova pronta), que usa o mesmo notch (spec da atualização §5.3).
public enum TipoAviso { Terminou, Esperando, Atualizacao }

/// Aviso do Claude Code para o notch (spec do notch vivo §4.3): vem do gancho (Stop ou Notification) e viaja pelo canal
/// como uma linha JSON.
public sealed partial record AvisoClaude(TipoAviso Tipo, string Sessao, string Texto)
{
    /// Cabe a mensagem inteira do Claude (até 32 KB de texto, com folga para o escape do JSON).
    public const int MaximoLinha = 65536;

    public const int MaximoCompleto = 32_768;

    /// A última resposta inteira do Claude, legível (spec da resposta §3.2); null sem transcrição.
    public string? Completo { get; init; }

    /// A janela da frente não era do app do Claude quando o aviso saiu: um "esperando você" entra nas pendências.
    public bool ForaDoApp { get; init; }

    private static readonly JsonSerializerOptions OpcoesLinha = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// O id da sessão do Claude (session_id): descarta só os pedidos dessa mesma sessão, mesmo com outras na pasta.
    public string? SessaoId { get; init; }

    /// O que o gancho recebe na entrada padrão. <paramref name="lerFimDoArquivo"/> devolve os últimos 64 KB da
    /// transcrição (ou null). Evento que não interessa, JSON vazio ou com defeito: null.
    public static AvisoClaude? DeGancho(string json, Func<string, string?> lerFimDoArquivo, bool foraDoApp = false)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;
            TipoAviso? tipo = Campo(raiz, "hook_event_name") switch
            {
                "Stop" => TipoAviso.Terminou,
                "Notification" => TipoAviso.Esperando,
                _ => null,
            };
            if (tipo is null) return null;
            var pasta = Campo(raiz, "cwd")?.TrimEnd('\\', '/');
            var sessao = string.IsNullOrEmpty(pasta) ? "Claude" : Path.GetFileName(pasta);
            if (string.IsNullOrEmpty(sessao)) sessao = "Claude";
            // A última resposta do Claude: o resumo do Stop e, nos dois eventos, a mensagem inteira.
            string? resposta = null;
            if (Campo(raiz, "transcript_path") is { } caminho && lerFimDoArquivo(caminho) is { } trecho)
                resposta = UltimaRespostaDoAssistente(trecho);
            var texto = tipo == TipoAviso.Esperando ? Campo(raiz, "message") ?? "" : resposta ?? "";
            return new AvisoClaude(tipo.Value, sessao, Encurtar(texto))
            {
                SessaoId = Campo(raiz, "session_id"),
                Completo = string.IsNullOrEmpty(resposta) ? null : Completar(resposta),
                ForaDoApp = foraDoApp,
            };
        }
        catch (JsonException) { return null; }
    }

    private static string? Campo(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// A última mensagem do assistente que tem texto, num trecho JSONL da transcrição. A primeira linha do trecho pode
    /// vir cortada; linhas que não são JSON são ignoradas.
    public static string UltimaRespostaDoAssistente(string trechoJsonl)
    {
        var linhas = trechoJsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = linhas.Length - 1; i >= 0; i--)
        {
            try
            {
                using var doc = JsonDocument.Parse(linhas[i]);
                var raiz = doc.RootElement;
                if (Campo(raiz, "type") != "assistant" || !raiz.TryGetProperty("message", out var msg)
                    || !msg.TryGetProperty("content", out var conteudo) || conteudo.ValueKind != JsonValueKind.Array) continue;
                foreach (var bloco in conteudo.EnumerateArray())
                    if (Campo(bloco, "type") == "text" && Campo(bloco, "text") is { Length: > 0 } t) return t;
            }
            catch (JsonException) { /* linha cortada ou estranha: segue */ }
        }
        return "";
    }

    /// Uma linha só, sem a marcação Markdown simples, no máximo <paramref name="maximo"/> caracteres (com o "…").
    public static string Encurtar(string texto, int maximo = 80)
    {
        var limpo = Cabecalho().Replace(texto, "");
        limpo = limpo.Replace("*", "").Replace("`", "");
        limpo = Espacos().Replace(limpo, " ").Trim();
        return limpo.Length <= maximo ? limpo : limpo[..(maximo - 1)].TrimEnd() + "…";
    }

    /// O texto inteiro para ler no notch: sem o Markdown simples (títulos, negrito, código, marcadores de lista), com as
    /// linhas e os parágrafos mantidos (no máximo uma linha em branco), até <see cref="MaximoCompleto"/> com "…".
    public static string Completar(string texto)
    {
        var linhas = texto.Replace("\r", "").Split('\n').Select(l =>
        {
            var limpa = Marcador().Replace(Cabecalho().Replace(l, ""), "");
            return Espacos().Replace(limpa.Replace("*", "").Replace("`", ""), " ").Trim();
        });
        var junto = Paragrafos().Replace(string.Join("\n", linhas), "\n\n").Trim();
        return junto.Length <= MaximoCompleto ? junto : junto[..(MaximoCompleto - 1)] + "…";
    }

    [GeneratedRegex(@"(?m)^\s*#+\s*")] private static partial Regex Cabecalho();
    [GeneratedRegex(@"^\s*(?:[-*+]|\d+\.)\s+")] private static partial Regex Marcador();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex Paragrafos();
    [GeneratedRegex(@"\s+")] private static partial Regex Espacos();

    /// A linha que viaja pelo canal.
    public string ParaLinha() => JsonSerializer.Serialize(new Linha(Tipo.ToString(), Sessao, Texto, SessaoId, Completo, ForaDoApp), OpcoesLinha);

    public static AvisoClaude? DeLinha(string? linha)
    {
        if (string.IsNullOrWhiteSpace(linha) || linha.Length > MaximoLinha) return null;
        try
        {
            var l = JsonSerializer.Deserialize<Linha>(linha, OpcoesLinha);
            return l is { Sessao: not null, Texto: not null } && Enum.TryParse<TipoAviso>(l.Tipo, out var tipo)
                ? new AvisoClaude(tipo, l.Sessao, l.Texto) { SessaoId = l.SessaoId, Completo = l.Completo, ForaDoApp = l.ForaDoApp } : null;
        }
        catch (JsonException) { return null; }
    }

    private sealed record Linha(string? Tipo, string? Sessao, string? Texto, string? SessaoId, string? Completo, bool ForaDoApp);
}

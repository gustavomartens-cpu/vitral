using System.Text.Json;

namespace Vitral.Core.Claude;

/// De onde veio o turno que acabou de terminar (R4 da pesquisa de notches): só a mensagem que o Gustavo digitou avisa
/// "Claude terminou". O Claude Code (conferido na 2.1.325) grava na mensagem que abre cada turno o campo "turnOrigin":
/// "human" para o que foi digitado; "task_notification" (tarefa em segundo plano ou subagente que voltou), "peer"
/// (mensagem de outra sessão), "host_synthetic" (sessão aberta por outra), "sdk" e "scheduled" para o resto. Uma
/// mensagem digitada no meio de um turno entra como anexo "queued_command", com a origem em "origin.kind".
/// <paramref name="Mensagem"/> é o uuid do registro: identifica a mensagem para avisar uma vez só por ela.
public sealed record TurnoClaude(string Origem, string Mensagem)
{
    public const string Humano = "human";

    public bool DoGustavo => Origem == Humano;

    /// O registro mais recente que diz a origem, num trecho JSONL da transcrição; null se o trecho não tem nenhum (uma
    /// versão do Claude Code sem o campo, ou um turno maior que o trecho). A primeira linha pode vir cortada.
    public static TurnoClaude? Ultimo(string trechoJsonl)
    {
        var linhas = trechoJsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = linhas.Length - 1; i >= 0; i--)
        {
            var linha = linhas[i];
            // Só abre o JSON das linhas que podem ter a origem (a transcrição tem muitas linhas grandes).
            if (!linha.Contains("\"turnOrigin\"", StringComparison.Ordinal) && !linha.Contains("\"queued_command\"", StringComparison.Ordinal)) continue;
            try
            {
                using var doc = JsonDocument.Parse(linha);
                var raiz = doc.RootElement;
                if (raiz.ValueKind != JsonValueKind.Object) continue;
                // O texto "turnOrigin" dentro de um resultado de ferramenta não conta: só o campo do próprio registro.
                if (raiz.TryGetProperty("isSidechain", out var lateral) && lateral.ValueKind == JsonValueKind.True) continue;
                var uuid = Texto(raiz, "uuid") ?? "";
                if (Texto(raiz, "turnOrigin") is { Length: > 0 } origem) return new(origem, uuid);
                if (Texto(raiz, "type") == "attachment" && raiz.TryGetProperty("attachment", out var anexo) && anexo.ValueKind == JsonValueKind.Object
                    && Texto(anexo, "type") == "queued_command" && anexo.TryGetProperty("origin", out var de) && de.ValueKind == JsonValueKind.Object
                    && Texto(de, "kind") is { Length: > 0 } tipo)
                    return new(tipo, uuid);
            }
            catch (JsonException) { /* linha cortada ou estranha: segue */ }
        }
        return null;
    }

    /// O Ctrl+C não dispara o Stop (R1): o Claude Code grava uma mensagem do usuário "[Request interrupted by user…]".
    /// Verdadeiro quando a última mensagem (de usuário ou do assistente) do trecho é essa marca.
    public static bool Interrompido(string trechoJsonl)
    {
        var linhas = trechoJsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = linhas.Length - 1; i >= 0; i--)
        {
            try
            {
                using var doc = JsonDocument.Parse(linhas[i]);
                var raiz = doc.RootElement;
                if (raiz.ValueKind != JsonValueKind.Object) continue;
                var tipo = Texto(raiz, "type");
                if (tipo == "assistant") return false;
                if (tipo != "user" || !raiz.TryGetProperty("message", out var msg) || msg.ValueKind != JsonValueKind.Object
                    || !msg.TryGetProperty("content", out var conteudo)) continue;
                if (conteudo.ValueKind == JsonValueKind.String) return EhMarca(conteudo.GetString());
                if (conteudo.ValueKind != JsonValueKind.Array) continue;
                foreach (var bloco in conteudo.EnumerateArray())
                    if (bloco.ValueKind == JsonValueKind.Object && Texto(bloco, "type") == "text") return EhMarca(Texto(bloco, "text"));
                return false; // resultado de ferramenta: o turno seguia
            }
            catch (JsonException) { /* linha cortada ou estranha: segue */ }
        }
        return false;
    }

    private static bool EhMarca(string? texto) => texto?.StartsWith("[Request interrupted by user", StringComparison.Ordinal) == true;

    private static string? Texto(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// O Gustavo saiu da mesa (R5 da pesquisa de notches): sem teclado e mouse há <see cref="Limite"/>, o pedido de permissão
/// e a pergunta do Claude não esperam o notch; vão direto para o app do Claude, que os guarda até a volta.
public static class Ausencia
{
    public static readonly TimeSpan Limite = TimeSpan.FromMinutes(5);

    /// <paramref name="semUso"/>: há quanto tempo não há teclado nem mouse (null se não deu para ler: conta como presente).
    public static bool Ausente(TimeSpan? semUso) => semUso >= Limite;
}

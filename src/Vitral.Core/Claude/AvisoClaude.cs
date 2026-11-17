using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vitral.Core.Claude;

/// Atualizacao: aviso do próprio Vitral (versão nova pronta), que usa o mesmo notch (spec da atualização §5.3).
/// Comecou: o Gustavo mandou uma mensagem (gancho UserPromptSubmit); não desce, só conta a sessão como rodando (R1).
public enum TipoAviso { Terminou, Esperando, Atualizacao, Comecou }

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

    /// A conversa do aviso estava aberta e à vista no app do Claude: o notch não avisa (o Gustavo já está olhando).
    public bool AVista { get; init; }

    /// A conversa no app do Claude ("local_…"), para o clique abrir exatamente ela (ver <see cref="ConversaClaude"/>).
    public string? AppId { get; init; }

    private static readonly JsonSerializerOptions OpcoesLinha = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// O id da sessão do Claude (session_id): descarta só os pedidos dessa mesma sessão, mesmo com outras na pasta.
    public string? SessaoId { get; init; }

    /// De onde veio o turno que terminou (só no Stop; ver <see cref="TurnoClaude"/>): null quando não deu para saber.
    public string? Origem { get; init; }

    /// A mensagem que abriu o turno (o uuid dela na transcrição): o notch avisa uma vez só por mensagem.
    public string? Mensagem { get; init; }

    /// Fim de um turno que o Gustavo não pediu (tarefa em segundo plano, subagente, outra sessão, continuação
    /// automática): não desce no notch. Sem a origem (versão do Claude Code sem o campo), avisa como antes.
    /// A transcrição da sessão (transcript_path): o notch confere se ela parou ou foi interrompida (R1).
    public string? Transcricao { get; init; }

    /// O tipo do Notification ("permission_prompt", "idle_prompt"…): o ocioso não pinta a sessão de âmbar (R1).
    public string? TipoNotificacao { get; init; }

    /// A sessão, para contar: o id dela, ou a pasta quando o Claude Code não manda o id.
    public string ChaveSessao => SessaoId ?? Sessao;

    public bool DeOutraOrigem => Tipo == TipoAviso.Terminou && Origem is not null && Origem != TurnoClaude.Humano;

    /// O que o gancho recebe na entrada padrão. <paramref name="lerFimDoArquivo"/> devolve o fim da transcrição (ou
    /// null); <paramref name="lerMais"/>, um fim maior, lido só quando o primeiro não alcança a mensagem que abriu o
    /// turno. Evento que não interessa, JSON vazio ou com defeito: null.
    public static AvisoClaude? DeGancho(string json, Func<string, string?> lerFimDoArquivo, bool foraDoApp = false, Func<string, string?>? lerMais = null)
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
                "UserPromptSubmit" => TipoAviso.Comecou,
                _ => null,
            };
            if (tipo is null) return null;
            var pasta = Campo(raiz, "cwd")?.TrimEnd('\\', '/');
            var sessao = string.IsNullOrEmpty(pasta) ? "Claude" : Path.GetFileName(pasta);
            if (string.IsNullOrEmpty(sessao)) sessao = "Claude";
            // A última resposta do Claude: o resumo do Stop e, nos dois eventos, a mensagem inteira.
            // O começo não lê a transcrição: o gancho é assíncrono e só marca a sessão como rodando.
            if (tipo == TipoAviso.Comecou)
                return new AvisoClaude(TipoAviso.Comecou, sessao, "") { SessaoId = Campo(raiz, "session_id"), Transcricao = Campo(raiz, "transcript_path"), ForaDoApp = foraDoApp };
            string? resposta = null;
            TurnoClaude? turno = null;
            if (Campo(raiz, "transcript_path") is { } caminho && lerFimDoArquivo(caminho) is { } trecho)
            {
                resposta = UltimaRespostaDoAssistente(trecho);
                if (tipo == TipoAviso.Terminou)
                    turno = TurnoClaude.Ultimo(trecho) ?? (lerMais?.Invoke(caminho) is { } maior ? TurnoClaude.Ultimo(maior) : null);
            }
            var texto = tipo == TipoAviso.Esperando ? Campo(raiz, "message") ?? "" : resposta ?? "";
            return new AvisoClaude(tipo.Value, sessao, Encurtar(texto))
            {
                SessaoId = Campo(raiz, "session_id"),
                Completo = string.IsNullOrEmpty(resposta) ? null : Completar(resposta),
                ForaDoApp = foraDoApp,
                Origem = turno?.Origem,
                Mensagem = turno?.Mensagem,
                Transcricao = Campo(raiz, "transcript_path"),
                TipoNotificacao = Campo(raiz, "notification_type"),
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
    // Só os marcadores sem significado; "1." fica, para o "faz a 2" ter a que se referir.
    [GeneratedRegex(@"^\s*[-*+]\s+")] private static partial Regex Marcador();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex Paragrafos();
    [GeneratedRegex(@"\s+")] private static partial Regex Espacos();

    /// A linha que viaja pelo canal.
    public string ParaLinha() => JsonSerializer.Serialize(new Linha(Tipo.ToString(), Sessao, Texto, SessaoId, Completo, ForaDoApp, AppId, AVista, Origem, Mensagem, Transcricao, TipoNotificacao), OpcoesLinha);

    public static AvisoClaude? DeLinha(string? linha)
    {
        if (string.IsNullOrWhiteSpace(linha) || linha.Length > MaximoLinha) return null;
        try
        {
            var l = JsonSerializer.Deserialize<Linha>(linha, OpcoesLinha);
            return l is { Sessao: not null, Texto: not null } && Enum.TryParse<TipoAviso>(l.Tipo, out var tipo)
                ? new AvisoClaude(tipo, l.Sessao, l.Texto) { SessaoId = l.SessaoId, Completo = l.Completo, ForaDoApp = l.ForaDoApp, AppId = l.AppId, AVista = l.AVista, Origem = l.Origem, Mensagem = l.Mensagem, Transcricao = l.Transcricao, TipoNotificacao = l.TipoNotificacao } : null;
        }
        catch (JsonException) { return null; }
    }

    private sealed record Linha(string? Tipo, string? Sessao, string? Texto, string? SessaoId, string? Completo, bool ForaDoApp, string? AppId,
        bool AVista = false, string? Origem = null, string? Mensagem = null, string? Transcricao = null, string? TipoNotificacao = null);
}

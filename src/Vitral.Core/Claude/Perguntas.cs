using System.Text.Json.Nodes;

namespace Vitral.Core.Claude;

/// Uma opção de uma pergunta do Claude: o rótulo é o que volta como resposta; a descrição vai para a dica do mouse.
public sealed record Opcao(string Rotulo, string? Descricao);

/// Uma pergunta de escolha do Claude (`AskUserQuestion`, spec das perguntas §2).
public sealed record Pergunta(string Texto, string? Cabecalho, IReadOnlyList<Opcao> Opcoes, bool Multipla);

/// A resposta de uma ficha do notch: a escolha e, numa pergunta, o rótulo escolhido de cada pergunta (pelo texto dela).
public sealed record RespostaPedido(EscolhaPermissao Escolha, IReadOnlyDictionary<string, string>? Respostas = null)
{
    public static implicit operator RespostaPedido(EscolhaPermissao escolha) => new(escolha);
}

/// As regras das perguntas no notch que não dependem da tela (spec das perguntas §3 e §5).
public static class RegrasPerguntas
{
    public const string Ferramenta = "AskUserQuestion";

    /// A ficha mostra as opções só com 1 a 4 perguntas de 1 a 4 opções cada; senão, só "Responder na conversa".
    public static bool Cabe(IReadOnlyList<Pergunta>? perguntas) =>
        perguntas is { Count: >= 1 and <= 4 } && perguntas.All(p => p.Opcoes.Count is >= 1 and <= 4);

    /// As escolhas de uma pergunta múltipla, na ordem das opções e juntas com ", " (o formato provado).
    public static string Juntar(Pergunta pergunta, IEnumerable<string> rotulos)
    {
        var escolhidos = rotulos.ToHashSet(StringComparer.Ordinal);
        return string.Join(", ", pergunta.Opcoes.Select(o => o.Rotulo).Where(escolhidos.Contains));
    }

    /// As perguntas do `tool_input`; nulo se o formato não é o esperado (a ficha cai no "não cabe").
    internal static IReadOnlyList<Pergunta>? Ler(JsonNode? entrada)
    {
        if (entrada?["questions"] is not JsonArray lista) return null;
        var perguntas = new List<Pergunta>();
        foreach (var q in lista)
        {
            if (PedidoPermissao.Texto(q?["question"]) is not { } texto) return null;
            var opcoes = new List<Opcao>();
            foreach (var o in q!["options"] as JsonArray ?? [])
            {
                if (PedidoPermissao.Texto(o?["label"]) is not { } rotulo) return null;
                opcoes.Add(new Opcao(rotulo, PedidoPermissao.Texto(o!["description"])));
            }
            bool multipla = q["multiSelect"] is JsonValue v && v.TryGetValue<bool>(out var m) && m;
            perguntas.Add(new Pergunta(texto, PedidoPermissao.Texto(q["header"]), opcoes, multipla));
        }
        return perguntas;
    }
}

using Vitral.Core.Claude;

namespace Vitral.Ui.Barra;

/// Sessões do Claude que ficaram sem resposta pelo notch (spec da resposta §4.1): a lista que desce do notch, a mais
/// nova em cima, uma por sessão. Vive enquanto o Vitral está aberto.
public sealed class Pendencias
{
    public sealed record Pendencia(string? SessaoId, string Sessao, TipoAviso Tipo, DateTime Hora, string Completo);

    private readonly List<Pendencia> _itens = [];

    public IReadOnlyList<Pendencia> Itens => _itens;
    public int Quantas => _itens.Count;
    public event Action? Mudou;

    /// Entra em cima; a pendência anterior da mesma sessão sai.
    public void Entrou(Pendencia p)
    {
        Remover(p.SessaoId, p.Sessao);
        _itens.Insert(0, p);
        Mudou?.Invoke();
    }

    public void Saiu(Pendencia p)
    {
        if (_itens.Remove(p)) Mudou?.Invoke();
    }

    /// A sessão mandou algo novo (aviso ou ficha): a pendência antiga dela não vale mais.
    public void SaiuDaSessao(string? sessaoId, string sessao)
    {
        if (Remover(sessaoId, sessao)) Mudou?.Invoke();
    }

    /// Com o id da sessão dos dois lados, só ele vale (várias conversas na mesma pasta); sem ele, o nome da pasta.
    internal static bool MesmaSessao(string? idA, string sessaoA, string? idB, string sessaoB) =>
        idA is not null && idB is not null ? idA == idB : sessaoA == sessaoB;

    private bool Remover(string? sessaoId, string sessao) =>
        _itens.RemoveAll(x => MesmaSessao(x.SessaoId, x.Sessao, sessaoId, sessao)) > 0;
}

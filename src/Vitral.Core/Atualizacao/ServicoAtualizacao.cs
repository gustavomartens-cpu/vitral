using System.Net;

namespace Vitral.Core.Atualizacao;

/// De onde vem a atualização (o App usa o Velopack com as releases do GitHub). Falhas chegam como exceção.
public interface IFonteAtualizacao
{
    /// Há token e o Vitral está instalado (builds de desenvolvimento ficam desligados).
    bool Ligada { get; }

    /// A versão nova, ou null se a instalada já é a mais nova.
    Task<string?> ProcurarAsync();

    Task BaixarAsync();

    void Reiniciar();
}

public enum EstadoAtualizacao { Desligada, Atualizado, Baixando, Pronta, Falhou, AcessoNegado }

/// Quando procurar, baixar e avisar (spec da atualização §5.1): 1 min depois de abrir e a cada 6 h; uma versão nova é
/// baixada em segundo plano e avisada uma vez; falha nunca avisa, só muda o estado e tenta no próximo ciclo.
public sealed class ServicoAtualizacao
{
    public static readonly TimeSpan PrimeiraConferencia = TimeSpan.FromMinutes(1), Intervalo = TimeSpan.FromHours(6);

    private readonly IFonteAtualizacao _fonte;
    private readonly Func<DateTime> _agora;
    private DateTime _proxima;
    private bool _ocupado;

    public ServicoAtualizacao(IFonteAtualizacao fonte, Func<DateTime> agora)
    {
        _fonte = fonte;
        _agora = agora;
        _proxima = agora() + PrimeiraConferencia;
        Estado = fonte.Ligada ? EstadoAtualizacao.Atualizado : EstadoAtualizacao.Desligada;
    }

    public EstadoAtualizacao Estado { get; private set; }
    public string? Versao { get; private set; }
    public event Action? Mudou;

    /// Uma versão nova foi baixada e está pronta para entrar (dispara uma vez por versão).
    public event Action<string>? Pronta;

    /// Chamado pelo App a cada minuto.
    public Task TiqueAsync() => _agora() >= _proxima ? ProcurarAgoraAsync() : Task.CompletedTask;

    /// O botão "Procurar agora" (e o ciclo).
    public async Task ProcurarAgoraAsync()
    {
        if (!_fonte.Ligada || Estado == EstadoAtualizacao.Pronta || _ocupado) return;
        _ocupado = true;
        _proxima = _agora() + Intervalo;
        try
        {
            var nova = await _fonte.ProcurarAsync();
            if (nova is null) { Ir(EstadoAtualizacao.Atualizado, Versao); return; }
            Ir(EstadoAtualizacao.Baixando, nova);
            await _fonte.BaixarAsync();
            Ir(EstadoAtualizacao.Pronta, nova);
            Pronta?.Invoke(nova);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Ir(AcessoNegado(e) ? EstadoAtualizacao.AcessoNegado : EstadoAtualizacao.Falhou, Versao);
        }
        finally { _ocupado = false; }
    }

    public void Reiniciar() => _fonte.Reiniciar();

    private void Ir(EstadoAtualizacao estado, string? versao)
    {
        Estado = estado;
        Versao = versao;
        Mudou?.Invoke();
    }

    /// 401/403 em qualquer nível da exceção (o Velopack embrulha a do HTTP).
    private static bool AcessoNegado(Exception? e)
    {
        for (; e is not null; e = e.InnerException)
            if (e is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }) return true;
        return false;
    }
}

/// A linha de estado na janela principal (spec §5.4).
public static class TextoAtualizacao
{
    public static string Para(EstadoAtualizacao estado, string? versao) => estado switch
    {
        EstadoAtualizacao.Desligada => "Atualização desligada (versão de desenvolvimento)",
        EstadoAtualizacao.Atualizado => "Atualizado",
        EstadoAtualizacao.Baixando => $"Baixando a versão {versao}…",
        EstadoAtualizacao.Pronta => $"Versão {versao} pronta — reiniciar",
        EstadoAtualizacao.AcessoNegado => "Não foi possível procurar atualizações (acesso negado: o token venceu?)",
        _ => "Não foi possível procurar atualizações",
    };
}

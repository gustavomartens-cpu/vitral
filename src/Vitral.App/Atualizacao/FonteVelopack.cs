using System.Windows;
using Velopack;
using Velopack.Sources;
using Vitral.App.Barra;
using Vitral.Core.Atualizacao;

namespace Vitral.App.Atualizacao;

/// Atualização pelas releases do GitHub com o Velopack (spec da atualização §5.2). Só liga com o Vitral instalado; builds
/// de desenvolvimento ficam desligados. O repositório é público desde a 0.8.3: o token embutido, se houver, é usado, mas
/// não é mais preciso. Pré-releases são ignoradas.
internal sealed class FonteVelopack : IFonteAtualizacao
{
    private const string Repositorio = "https://github.com/gustavomartens-cpu/vitral";

    private readonly UpdateManager? _gerente;
    private readonly Action _limpezaDeSaida;
    private UpdateInfo? _info;

    public FonteVelopack(string? token, Action limpezaDeSaida)
    {
        _limpezaDeSaida = limpezaDeSaida;
        try
        {
            var gerente = new UpdateManager(new GithubSource(Repositorio, token, false, null), null, null);
            if (gerente.IsInstalled) _gerente = gerente;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("FonteVelopack", e); }
    }

    public bool Ligada => _gerente is not null;

    /// A versão instalada, se o Velopack souber (senão, a do assembly).
    public string VersaoAtual => _gerente?.CurrentVersion?.ToString()
        ?? typeof(FonteVelopack).Assembly.GetName().Version?.ToString(3) ?? "?";

    public async Task<string?> ProcurarAsync()
    {
        _info = await _gerente!.CheckForUpdatesAsync();
        return _info?.TargetFullRelease.Version.ToString();
    }

    public Task BaixarAsync() => _gerente!.DownloadUpdatesAsync(_info!, null, default);

    /// Agenda a troca (o Update.exe espera o Vitral sair) e só então faz a limpeza de saída de sempre (barra do Windows,
    /// ícones, doca) e fecha. Se a troca não puder ser agendada (Update.exe bloqueado, pacote sumido), nada é desfeito
    /// e o Vitral segue aberto.
    public bool Reiniciar()
    {
        if (_gerente is null || _info is null) return false;
        try { _gerente.WaitExitThenApplyUpdates(_info.TargetFullRelease, true, true, []); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("FonteVelopack.Reiniciar", e);
            return false;
        }
        _limpezaDeSaida();
        Application.Current.Shutdown();
        return true;
    }
}

using System.Windows;
using Velopack;
using Velopack.Sources;
using Vitral.App.Barra;
using Vitral.Core.Atualizacao;

namespace Vitral.App.Atualizacao;

/// Atualização pelas releases do GitHub com o Velopack (spec da atualização §5.2). Só liga com o token embutido e com o
/// Vitral instalado; builds de desenvolvimento ficam desligados. Pré-releases são ignoradas.
internal sealed class FonteVelopack : IFonteAtualizacao
{
    private const string Repositorio = "https://github.com/gustavomartens-cpu/vitral";

    private readonly UpdateManager? _gerente;
    private readonly Action _limpezaDeSaida;
    private UpdateInfo? _info;

    public FonteVelopack(string? token, Action limpezaDeSaida)
    {
        _limpezaDeSaida = limpezaDeSaida;
        if (token is null) return;
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

    /// A limpeza de saída de sempre (barra do Windows, ícones, doca) e depois a troca da versão, que reabre o Vitral.
    public void Reiniciar()
    {
        if (_gerente is null || _info is null) return;
        _limpezaDeSaida();
        _gerente.WaitExitThenApplyUpdates(_info.TargetFullRelease, true, true, []);
        Application.Current.Shutdown();
    }
}

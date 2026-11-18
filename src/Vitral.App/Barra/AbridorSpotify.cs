using System.Diagnostics;
using System.IO;
using Vitral.Ui.Barra;
using Windows.Media.Control;
using Gerente = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Vitral.App.Barra;

/// Abre o Spotify e retoma a música (spec da prévia do Spotify §2): o URI spotify: (o app da Store e o instalador comum
/// o registram; sem ele, o Spotify.exe do instalador), depois espera a sessão do Spotify aparecer nos controles de mídia
/// do Windows e manda tocar. O Spotify restaura sozinho a última faixa pausada.
/// Pelo play, o Spotify abre minimizado (Gustavo, 17/11/2026): a janela é minimizada quando aparece, se foi o play que
/// o abriu (já aberto, ou pelo clique no título, a janela fica como está).
internal sealed partial class AbridorSpotify : IAbridorSpotify
{
    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr janela, int comando);

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr janela);

    // SW_SHOWMINNOACTIVE: minimiza sem ativar outra janela (o SW_MINIMIZE ativava a próxima da fila, e uma janela
    // minimizada do Vitral voltava à tela).
    private const int SwMinimizar = 7;
    private const string Processo = "Spotify";

    /// O Spotify restaura a janela sozinho quando termina de carregar: depois de minimizada, ela é vigiada mais um pouco.
    private static readonly TimeSpan Vigia = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(500);

    public void Abrir() => _ = Task.Run(Lancar); // o ShellExecute pode segurar a interface

    public async Task<bool> AbrirETocarAsync(CancellationToken cancelar)
    {
        try
        {
            // Fora da thread da interface (o ShellExecute pode segurá-la); o await volta a ela.
            return await Task.Run(async () =>
            {
                bool minimizar = !SpotifyAberto();
                return Lancar() && await EsperarETocarAsync(minimizar, cancelar);
            }, cancelar);
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception e) { LogBarra.Erro("Spotify.Tocar", e); return false; }
    }

    private static bool Lancar()
    {
        try
        {
            Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception e) { LogBarra.Erro("Spotify.Abrir.Uri", e); }
        try
        {
            string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe");
            if (!File.Exists(exe)) return false;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception e) { LogBarra.Erro("Spotify.Abrir.Exe", e); return false; }
    }

    private static bool SpotifyAberto()
    {
        var processos = Process.GetProcessesByName(Processo);
        foreach (var p in processos) p.Dispose();
        return processos.Length > 0;
    }

    /// Minimiza a janela principal do Spotify; false enquanto ela ainda não apareceu.
    private static bool Minimizar()
    {
        var processos = Process.GetProcessesByName(Processo);
        try
        {
            var janela = processos.Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);
            if (janela == IntPtr.Zero) return false;
            if (!IsIconic(janela))
            {
                ShowWindow(janela, SwMinimizar);
                LogBarra.Info($"Spotify minimizado (janela {janela})");
            }
            return true;
        }
        catch (Exception e) { LogBarra.Erro("Spotify.Minimizar", e); return true; } // não insiste
        finally { foreach (var p in processos) p.Dispose(); }
    }

    /// Procura a sessão do Spotify entre todas (a atual do Windows pode ser outra) e manda tocar até ela tocar.
    private static async Task<bool> EsperarETocarAsync(bool minimizar, CancellationToken cancelar)
    {
        var gerente = await Gerente.RequestAsync();
        var fim = DateTime.UtcNow + Prazo;
        bool tocou = false;
        DateTime? minimizadaEm = null;
        while (DateTime.UtcNow < fim)
        {
            cancelar.ThrowIfCancellationRequested();
            if (minimizar && Minimizar()) minimizadaEm ??= DateTime.UtcNow;
            if (minimizadaEm is { } m && DateTime.UtcNow - m > Vigia) minimizar = false;
            try
            {
                var sessao = gerente.GetSessions().FirstOrDefault(s =>
                    FonteMidiaWindows.NomeDoApp(s.SourceAppUserModelId) == FonteMidiaComPrevia.NomeSpotify);
                if (sessao is not null)
                {
                    tocou = sessao.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    if (tocou && !minimizar) return true; // tocando e a janela já minimizada
                    if (!tocou) await sessao.TryPlayAsync();
                }
            }
            catch (Exception e) { LogBarra.Erro("Spotify.Sessao", e); }
            await Task.Delay(Intervalo, cancelar);
        }
        return tocou; // a janela não apareceu a tempo, mas o que vale é tocar
    }
}

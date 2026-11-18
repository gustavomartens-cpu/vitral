using System.Diagnostics;
using System.IO;
using Vitral.Ui.Barra;
using Windows.Media.Control;
using Gerente = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Vitral.App.Barra;

/// Abre o Spotify e retoma a música (spec da prévia do Spotify §2): o URI spotify: (o app da Store e o instalador comum
/// o registram; sem ele, o Spotify.exe do instalador), depois espera a sessão do Spotify aparecer nos controles de mídia
/// do Windows e manda tocar. O Spotify restaura sozinho a última faixa pausada.
internal sealed class AbridorSpotify : IAbridorSpotify
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(500);

    public void Abrir() => _ = Task.Run(Lancar); // o ShellExecute pode segurar a interface

    public async Task<bool> AbrirETocarAsync(CancellationToken cancelar)
    {
        try
        {
            // Fora da thread da interface (o ShellExecute pode segurá-la); o await volta a ela.
            return await Task.Run(async () => Lancar() && await EsperarETocarAsync(cancelar), cancelar);
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

    /// Procura a sessão do Spotify entre todas (a atual do Windows pode ser outra) e manda tocar até ela tocar.
    private static async Task<bool> EsperarETocarAsync(CancellationToken cancelar)
    {
        var gerente = await Gerente.RequestAsync();
        var fim = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < fim)
        {
            cancelar.ThrowIfCancellationRequested();
            try
            {
                var sessao = gerente.GetSessions().FirstOrDefault(s =>
                    FonteMidiaWindows.NomeDoApp(s.SourceAppUserModelId) == FonteMidiaComPrevia.NomeSpotify);
                if (sessao is not null)
                {
                    if (sessao.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        return true;
                    await sessao.TryPlayAsync();
                }
            }
            catch (Exception e) { LogBarra.Erro("Spotify.Sessao", e); }
            await Task.Delay(Intervalo, cancelar);
        }
        return false;
    }
}

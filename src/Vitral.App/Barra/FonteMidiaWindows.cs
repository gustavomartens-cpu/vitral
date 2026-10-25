using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Vitral.Ui.Barra;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Sessao = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using Gerente = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Vitral.App.Barra;

/// Mídia tocando pelos controles de mídia do Windows (Spotify, navegador...). Sessão pausada continua
/// visível (para dar play); sem sessão, Atual é null e a ilha some.
internal sealed partial class FonteMidiaWindows : IFonteMidia, IDisposable
{
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetForegroundWindow(IntPtr janela);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool ShowWindow(IntPtr janela, int comando);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsIconic(IntPtr janela);

    private readonly Dispatcher _ui;
    private Gerente? _gerente;
    private Sessao? _sessao;
    private bool _descartada;

    public EstadoMidia? Atual { get; private set; }
    public event Action? Mudou;

    public FonteMidiaWindows(Dispatcher ui)
    {
        _ui = ui;
        _ = IniciarAsync();
    }

    private async Task IniciarAsync()
    {
        try
        {
            _gerente = await Gerente.RequestAsync();
            if (_descartada) return;
            _gerente.CurrentSessionChanged += AoTrocarSessao;
            TrocarSessao();
        }
        catch (Exception e) { LogBarra.Erro("Midia.Iniciar", e); }
    }

    private void AoTrocarSessao(Gerente g, CurrentSessionChangedEventArgs a) => TrocarSessao();

    private void TrocarSessao()
    {
        try
        {
            if (_sessao is not null)
            {
                _sessao.MediaPropertiesChanged -= AoMudarPropriedades;
                _sessao.PlaybackInfoChanged -= AoMudarReproducao;
            }
            _sessao = _gerente?.GetCurrentSession();
            if (_sessao is not null)
            {
                _sessao.MediaPropertiesChanged += AoMudarPropriedades;
                _sessao.PlaybackInfoChanged += AoMudarReproducao;
            }
        }
        catch (Exception e) { LogBarra.Erro("Midia.TrocarSessao", e); _sessao = null; }
        _ = AtualizarAsync();
    }

    private void AoMudarPropriedades(Sessao s, MediaPropertiesChangedEventArgs a) => _ = AtualizarAsync();
    private void AoMudarReproducao(Sessao s, PlaybackInfoChangedEventArgs a) => _ = AtualizarAsync();

    private async Task AtualizarAsync()
    {
        EstadoMidia? novo = null;
        try
        {
            if (_sessao is { } s)
            {
                var props = await s.TryGetMediaPropertiesAsync();
                var info = s.GetPlaybackInfo();
                novo = new EstadoMidia(props?.Title, props?.Artist, NomeDoApp(s.SourceAppUserModelId),
                    info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    await LerCapaAsync(props?.Thumbnail));
            }
        }
        catch (Exception e) { LogBarra.Erro("Midia.Atualizar", e); }
        await _ui.InvokeAsync(() =>
        {
            if (_descartada) return;
            Atual = novo;
            Mudou?.Invoke();
        });
    }

    private static async Task<byte[]?> LerCapaAsync(IRandomAccessStreamReference? referencia)
    {
        if (referencia is null) return null;
        try
        {
            using var fluxo = await referencia.OpenReadAsync();
            using var leitor = new DataReader(fluxo);
            uint tamanho = (uint)fluxo.Size;
            await leitor.LoadAsync(tamanho);
            var bytes = new byte[tamanho];
            leitor.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception) { return null; } // capa é opcional
    }

    /// "Spotify.exe" → "Spotify"; "SpotifyAB.SpotifyMusic_x!Spotify" → "Spotify"; "MSEdge" fica.
    internal static string NomeDoApp(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return "Mídia";
        string nome = aumid.Contains('!') ? aumid[(aumid.LastIndexOf('!') + 1)..] : aumid;
        if (nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) nome = nome[..^4];
        return nome.Length == 0 ? "Mídia" : char.ToUpperInvariant(nome[0]) + nome[1..];
    }

    public void Anterior() => Comando(s => s.TrySkipPreviousAsync().AsTask());
    public void TocarPausar() => Comando(s => s.TryTogglePlayPauseAsync().AsTask());
    public void Proxima() => Comando(s => s.TrySkipNextAsync().AsTask());

    private void Comando(Func<Sessao, Task<bool>> acao)
    {
        if (_sessao is not { } s) return;
        _ = Task.Run(async () =>
        {
            try { await acao(s); }
            catch (Exception e) { LogBarra.Erro("Midia.Comando", e); }
        });
    }

    /// Traz para a frente a janela principal do processo que está tocando, quando ela existe.
    public void TrazerApp()
    {
        try
        {
            if (_sessao?.SourceAppUserModelId is not { } aumid) return;
            string processo = Path.GetFileNameWithoutExtension(aumid.Contains('!') ? aumid[(aumid.LastIndexOf('!') + 1)..] : aumid);
            var alvo = Process.GetProcessesByName(processo).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            if (alvo is null) return;
            if (IsIconic(alvo.MainWindowHandle)) ShowWindow(alvo.MainWindowHandle, 9); // SW_RESTORE
            SetForegroundWindow(alvo.MainWindowHandle);
        }
        catch (Exception e) { LogBarra.Erro("Midia.TrazerApp", e); }
    }

    public void Dispose()
    {
        _descartada = true;
        try
        {
            if (_gerente is not null) _gerente.CurrentSessionChanged -= AoTrocarSessao;
            if (_sessao is not null)
            {
                _sessao.MediaPropertiesChanged -= AoMudarPropriedades;
                _sessao.PlaybackInfoChanged -= AoMudarReproducao;
            }
        }
        catch (Exception e) { LogBarra.Erro("Midia.Dispose", e); }
    }
}

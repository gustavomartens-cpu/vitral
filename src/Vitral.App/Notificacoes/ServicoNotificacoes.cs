using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vitral.Core.Notificacoes;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Vitral.App.Notificacoes;

/// As notificações dos outros apps (spec das notificações §3): lidas pelo UserNotificationListener (precisa da identidade
/// de pacote e da permissão), relidas quando o Windows avisa uma mudança (no máximo uma vez por segundo), sem as do app do
/// Claude. Publica na thread da interface.
internal sealed class ServicoNotificacoes(Dispatcher ui, WindowsQuieto quieto) : IDisposable
{
    private UserNotificationListener? _ouvinte;
    private IReadOnlyList<NotificacaoApp> _atuais = [];
    private readonly Dictionary<string, Windows.ApplicationModel.AppInfo> _apps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageSource?> _icones = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _espera = new(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromSeconds(1) };
    private bool _lendo;

    public EstadoNotificacoes Estado { get; private set; } = EstadoNotificacoes.SemIdentidade;
    public IReadOnlyList<NotificacaoApp> Atuais => _atuais;

    /// O estado ou a lista mudou.
    public event Action<EstadoNotificacoes, IReadOnlyList<NotificacaoApp>>? Mudou;

    /// Chegaram notificações que não estavam na leitura anterior (para descer no notch).
    public event Action<IReadOnlyList<NotificacaoApp>>? Novas;

    /// Na thread da interface (o pedido de permissão do Windows precisa dela).
    public async Task Iniciar()
    {
        if (!PacoteVitral.TemIdentidade) { Publicar(EstadoNotificacoes.SemIdentidade); return; }
        try
        {
            _ouvinte = UserNotificationListener.Current;
            if (await _ouvinte.RequestAccessAsync() != UserNotificationListenerAccessStatus.Allowed)
            {
                Publicar(EstadoNotificacoes.SemPermissao);
                return;
            }
            _espera.Tick += async (_, _) => { _espera.Stop(); await Ler(avisar: true); };
            _ouvinte.NotificationChanged += (_, _) => ui.BeginInvoke(() => { if (!_espera.IsEnabled) _espera.Start(); });
            Estado = EstadoNotificacoes.Ligado;
            quieto.Calar([]);
            await Ler(avisar: false); // as que já estavam lá não descem no notch
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Barra.LogBarra.Erro("ServicoNotificacoes.Iniciar", e);
            Publicar(EstadoNotificacoes.SemPermissao);
        }
    }

    private async Task Ler(bool avisar)
    {
        if (_ouvinte is null || _lendo) return;
        _lendo = true;
        try
        {
            var lidas = await _ouvinte.GetNotificationsAsync(NotificationKinds.Toast);
            var lista = new List<NotificacaoApp>();
            foreach (var n in lidas)
            {
                string app = n.AppInfo?.AppUserModelId ?? "";
                if (n.AppInfo is { } info) _apps[app] = info;
                var textos = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements()
                    .Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
                lista.Add(new NotificacaoApp(n.Id, app, n.AppInfo?.DisplayInfo?.DisplayName ?? app,
                    textos.FirstOrDefault(), textos.Count > 1 ? string.Join(" ", textos.Skip(1)) : null, n.CreationTime));
            }
            var filtradas = RegrasNotificacoes.Filtrar(lista);
            var (novas, _) = RegrasNotificacoes.Diferenca(_atuais, filtradas);
            _atuais = filtradas;
            if (novas.Count > 0) quieto.Calar(novas.Select(n => n.AppId)); // um app novo: a próxima já vem quieta
            Mudou?.Invoke(Estado, _atuais);
            if (avisar && novas.Count > 0) Novas?.Invoke(novas);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("ServicoNotificacoes.Ler", e); }
        finally { _lendo = false; }
    }

    public async Task Limpar(uint id)
    {
        try { _ouvinte?.RemoveNotification(id); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("ServicoNotificacoes.Limpar", e); }
        await Ler(avisar: false);
    }

    public async Task LimparApp(string appId)
    {
        foreach (var n in _atuais.Where(n => string.Equals(n.AppId, appId, StringComparison.OrdinalIgnoreCase)).ToList())
            try { _ouvinte?.RemoveNotification(n.Id); } catch (Exception e) when (e is not OutOfMemoryException) { }
        await Ler(avisar: false);
    }

    /// Só as que o Vitral mostra: as do app do Claude ficam na central.
    public async Task LimparTudo()
    {
        foreach (var n in _atuais.ToList())
            try { _ouvinte?.RemoveNotification(n.Id); } catch (Exception e) when (e is not OutOfMemoryException) { }
        await Ler(avisar: false);
    }

    public static void AbrirApp(string appId)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{appId}") { UseShellExecute = true }); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("ServicoNotificacoes.AbrirApp", e); }
    }

    /// O ícone do app (32 px), guardado por app; null se o Windows não tem.
    public async Task<ImageSource?> Icone(string appId)
    {
        if (_icones.TryGetValue(appId, out var pronto)) return pronto;
        ImageSource? icone = null;
        try
        {
            if (_apps.TryGetValue(appId, out var info))
            {
                using var fluxo = await info.DisplayInfo.GetLogo(new Windows.Foundation.Size(32, 32)).OpenReadAsync();
                using var copia = new MemoryStream();
                await fluxo.AsStreamForRead().CopyToAsync(copia);
                copia.Position = 0;
                var imagem = new BitmapImage();
                imagem.BeginInit();
                imagem.CacheOption = BitmapCacheOption.OnLoad;
                imagem.StreamSource = copia;
                imagem.EndInit();
                imagem.Freeze();
                icone = imagem;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("ServicoNotificacoes.Icone", e); }
        return _icones[appId] = icone;
    }

    private void Publicar(EstadoNotificacoes estado)
    {
        Estado = estado;
        Mudou?.Invoke(estado, _atuais);
    }

    public void Dispose() => _espera.Stop();
}

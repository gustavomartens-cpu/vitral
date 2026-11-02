using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vitral.Core.Notificacoes;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Vitral.App.Notificacoes;

/// As notificações dos outros apps (spec das notificações §3): lidas pelo UserNotificationListener (precisa da identidade
/// de pacote e da permissão), relidas quando o Windows avisa uma mudança, sem as do app do Claude. A leitura roda fora da
/// thread da interface e publica nela. O balão do Windows só fica calado com a leitura funcionando e o notch na tela.
internal sealed class ServicoNotificacoes(Dispatcher ui, WindowsQuieto quieto) : IDisposable
{
    private UserNotificationListener? _ouvinte;
    private TypedEventHandler<UserNotificationListener, UserNotificationChangedEventArgs>? _aoMudar;
    private IReadOnlyList<NotificacaoApp> _atuais = [];
    private readonly Dictionary<string, Windows.ApplicationModel.AppInfo> _apps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageSource?> _icones = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _espera = new(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromSeconds(1) };
    private bool _lendo, _relerDepois, _avisarDepois, _leuOk, _encerrado;
    private DateTime _ultimaLeitura;

    public EstadoNotificacoes Estado { get; private set; } = EstadoNotificacoes.SemIdentidade;
    public IReadOnlyList<NotificacaoApp> Atuais => _atuais;

    /// O notch está na tela (o App liga a barra): sem ele, o balão do Windows não pode ficar calado.
    public Func<bool> NotchNaTela { get; set; } = () => false;

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
            // A primeira mudança relê na hora (o aviso desce junto com a notificação); as que vêm em seguida, dentro de 1 s,
            // esperam e viram uma leitura só.
            _espera.Tick += async (_, _) => { _espera.Stop(); await Ler(avisar: true); };
            _aoMudar = (_, _) => ui.BeginInvoke(async () =>
            {
                if (_encerrado || _espera.IsEnabled) return;
                if (DateTime.UtcNow - _ultimaLeitura >= _espera.Interval) await Ler(avisar: true);
                else _espera.Start();
            });
            _ouvinte.NotificationChanged += _aoMudar;
            Estado = EstadoNotificacoes.Ligado;
            await Ler(avisar: false); // as que já estavam lá não descem no notch
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Barra.LogBarra.Erro("ServicoNotificacoes.Iniciar", e);
            Publicar(EstadoNotificacoes.SemPermissao);
        }
    }

    /// Lê de novo. Uma mudança que chega no meio de uma leitura não se perde: vira outra leitura logo depois.
    private async Task Ler(bool avisar)
    {
        if (_ouvinte is null || _encerrado) return;
        if (_lendo) { _relerDepois = true; _avisarDepois |= avisar; return; }
        _lendo = true;
        _ultimaLeitura = DateTime.UtcNow;
        try
        {
            var ouvinte = _ouvinte;
            var (lista, apps) = await Task.Run(async () =>
            {
                var lidas = await ouvinte.GetNotificationsAsync(NotificationKinds.Toast);
                var itens = new List<NotificacaoApp>();
                var infos = new Dictionary<string, Windows.ApplicationModel.AppInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in lidas)
                {
                    string app = n.AppInfo?.AppUserModelId ?? "";
                    if (n.AppInfo is { } info) infos[app] = info;
                    var textos = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements()
                        .Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
                    itens.Add(new NotificacaoApp(n.Id, app, n.AppInfo?.DisplayInfo?.DisplayName ?? app,
                        textos.FirstOrDefault(), textos.Count > 1 ? string.Join(" ", textos.Skip(1)) : null, n.CreationTime));
                }
                return (itens, infos);
            });
            if (_encerrado) return;
            foreach (var (app, info) in apps) _apps[app] = info;
            var filtradas = RegrasNotificacoes.Filtrar(lista);
            var (novas, _) = RegrasNotificacoes.Diferenca(_atuais, filtradas);
            _atuais = filtradas;
            _leuOk = true;
            Sincronizar(novas.Select(n => n.AppId)); // um app novo: a próxima dele já vem quieta
            Mudou?.Invoke(Estado, _atuais);
            if (avisar && novas.Count > 0) Novas?.Invoke(novas);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Sem conseguir ler (permissão retirada, por exemplo), o Windows volta a mostrar o balão.
            Barra.LogBarra.Erro("ServicoNotificacoes.Ler", e);
            _leuOk = false;
            if (e is UnauthorizedAccessException) Estado = EstadoNotificacoes.SemPermissao;
            Sincronizar([]);
            Mudou?.Invoke(Estado, _atuais);
        }
        finally
        {
            _lendo = false;
            if (_relerDepois && !_encerrado)
            {
                bool avisarDeNovo = _avisarDepois;
                (_relerDepois, _avisarDepois) = (false, false);
                _ = ui.BeginInvoke(async () => await Ler(avisarDeNovo));
            }
        }
    }

    /// O balão do Windows calado só quando o Vitral de fato mostra as notificações; senão, devolvido. O ControleBarra
    /// chama quando o notch aparece ou some.
    public void Sincronizar(IEnumerable<string>? novosApps = null)
    {
        if (_encerrado) return;
        if (RegrasNotificacoes.DeveCalar(Estado, _leuOk, NotchNaTela())) quieto.Calar(novosApps ?? []);
        else quieto.Devolver();
    }

    // As limpezas avisam as novas também: uma notificação que chega junto com o × não pode ser engolida.
    public async Task Limpar(uint id)
    {
        try { _ouvinte?.RemoveNotification(id); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("ServicoNotificacoes.Limpar", e); }
        await Ler(avisar: true);
    }

    public async Task LimparApp(string appId)
    {
        foreach (var n in _atuais.Where(n => string.Equals(n.AppId, appId, StringComparison.OrdinalIgnoreCase)).ToList())
            try { _ouvinte?.RemoveNotification(n.Id); } catch (Exception e) when (e is not OutOfMemoryException) { }
        await Ler(avisar: true);
    }

    /// Só as que o Vitral mostra: as do app do Claude ficam na central.
    public async Task LimparTudo()
    {
        foreach (var n in _atuais.ToList())
            try { _ouvinte?.RemoveNotification(n.Id); } catch (Exception e) when (e is not OutOfMemoryException) { }
        await Ler(avisar: true);
    }

    public static void AbrirApp(string appId)
    {
        try
        {
            var abrir = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            abrir.ArgumentList.Add($"shell:AppsFolder\\{appId}");
            Process.Start(abrir);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("ServicoNotificacoes.AbrirApp", e); }
    }

    /// O ícone do app (32 px), guardado por app; null se o Windows não tem.
    public async Task<ImageSource?> Icone(string appId)
    {
        if (_icones.TryGetValue(appId, out var pronto)) return pronto;
        ImageSource? icone = null;
        try
        {
            // Apps sem logo (o PowerShell, alguns programas antigos) ficam sem ícone.
            if (_apps.TryGetValue(appId, out var info) && info.DisplayInfo?.GetLogo(new Size(32, 32)) is { } logo
                && await logo.OpenReadAsync() is { } aberto)
            {
                using var fluxo = aberto;
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

    /// Na saída: para de ler e de calar (uma notificação que chegue depois do Devolver não cala tudo de novo).
    public void Dispose()
    {
        _encerrado = true;
        _espera.Stop();
        try { if (_ouvinte is not null && _aoMudar is not null) _ouvinte.NotificationChanged -= _aoMudar; }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("ServicoNotificacoes.Dispose", e); }
    }
}

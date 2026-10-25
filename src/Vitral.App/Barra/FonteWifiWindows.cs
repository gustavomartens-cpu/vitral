using System.Diagnostics;
using System.Windows.Threading;
using Vitral.Ui.Barra;
using Windows.Devices.Geolocation;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;

namespace Vitral.App.Barra;

/// Wi-Fi pelas APIs públicas do Windows (spec v2 §5). Sem adaptador: TemAdaptador false. Sem a localização
/// permitida para apps da área de trabalho, o Windows não lista as redes por perto (PermissaoLocalizacao false).
internal sealed class FonteWifiWindows : IFonteWifi, IDisposable
{
    private readonly Dispatcher _ui;
    private WiFiAdapter? _adaptador;
    private Radio? _radio;
    private bool _permissaoLocalizacao = true;
    private volatile bool _descartada;
    private readonly UltimaAtualizacao _ultima = new();

    public EstadoWifi? Atual { get; private set; }
    public event Action? Mudou;

    public FonteWifiWindows(Dispatcher ui)
    {
        _ui = ui;
        _ = IniciarAsync();
    }

    private async Task IniciarAsync()
    {
        try
        {
            if (await WiFiAdapter.RequestAccessAsync() == WiFiAccessStatus.Allowed)
                _adaptador = (await WiFiAdapter.FindAllAdaptersAsync()).FirstOrDefault();
            if (await Radio.RequestAccessAsync() == RadioAccessStatus.Allowed)
                _radio = (await Radio.GetRadiosAsync()).FirstOrDefault(r => r.Kind == RadioKind.WiFi);
            _permissaoLocalizacao = await Geolocator.RequestAccessAsync() == GeolocationAccessStatus.Allowed;
            if (_descartada) return;
            if (_adaptador is not null) _adaptador.AvailableNetworksChanged += AoMudarRedes;
            if (_radio is not null) _radio.StateChanged += AoMudarRadio;
            NetworkInformation.NetworkStatusChanged += AoMudarRede;
        }
        catch (Exception e) { LogBarra.Erro("Wifi.Iniciar", e); }
        await LerAsync();
    }

    private void AoMudarRedes(WiFiAdapter a, object o) => _ = LerAsync();
    private void AoMudarRadio(Radio r, object o) => _ = LerAsync();
    private void AoMudarRede(object? o) => _ = LerAsync();

    public async Task AtualizarAsync()
    {
        try
        {
            _permissaoLocalizacao = await Geolocator.RequestAccessAsync() == GeolocationAccessStatus.Allowed;
            if (_adaptador is not null && _radio?.State != RadioState.Off) await _adaptador.ScanAsync();
        }
        catch (Exception e) { LogBarra.Erro("Wifi.Buscar", e); }
        await LerAsync();
    }

    /// Monta o estado a partir do último relatório do adaptador (sem nova busca).
    private async Task LerAsync()
    {
        int geracao = _ultima.Comecar();
        EstadoWifi? estado = null;
        try
        {
            bool ligado = _radio?.State == RadioState.On || (_radio is null && _adaptador is not null);
            if (_adaptador is null) estado = new EstadoWifi(false, false, _permissaoLocalizacao, []);
            else
            {
                string? conectada = null;
                var perfil = await _adaptador.NetworkAdapter.GetConnectedProfileAsync();
                if (perfil?.IsWlanConnectionProfile == true) conectada = perfil.WlanConnectionProfileDetails.GetConnectedSsid();
                var salvas = NetworkInformation.GetConnectionProfiles()
                    .Where(p => p.IsWlanConnectionProfile).Select(p => p.ProfileName)
                    .ToHashSet(StringComparer.Ordinal);
                var redes = ligado
                    ? _adaptador.NetworkReport.AvailableNetworks
                        .Where(r => !string.IsNullOrEmpty(r.Ssid))
                        .GroupBy(r => r.Ssid)
                        .Select(g => g.OrderByDescending(r => r.SignalBars).First())
                        .Select(r => new RedeWifi(r.Ssid, r.SignalBars, Protegida(r), salvas.Contains(r.Ssid), r.Ssid == conectada, Corporativa(r)))
                        .ToList()
                    : [];
                if (conectada is not null && redes.All(r => r.Nome != conectada))
                    redes.Add(new RedeWifi(conectada, 4, true, true, true, false)); // sem localização a lista vem vazia
                estado = new EstadoWifi(true, ligado, _permissaoLocalizacao, redes);
            }
        }
        catch (Exception e) { LogBarra.Erro("Wifi.Ler", e); }
        await _ui.InvokeAsync(() =>
        {
            if (_descartada || !_ultima.EhAtual(geracao)) return;
            Atual = estado;
            Mudou?.Invoke();
        });
    }

    private static bool Protegida(WiFiAvailableNetwork r) =>
        r.SecuritySettings.NetworkAuthenticationType is not (NetworkAuthenticationType.None or NetworkAuthenticationType.Open80211);

    private static bool Corporativa(WiFiAvailableNetwork r)
    {
        var tipo = r.SecuritySettings.NetworkAuthenticationType;
        return tipo is NetworkAuthenticationType.Wpa or NetworkAuthenticationType.Rsna
            || tipo.ToString().Contains("Enterprise", StringComparison.Ordinal);
    }

    public async Task<ResultadoConexao> ConectarAsync(string rede, string? senha)
    {
        try
        {
            if (_adaptador is null) return new ResultadoConexao(false, "Nenhum adaptador Wi-Fi.");
            var alvo = _adaptador.NetworkReport.AvailableNetworks.Where(r => r.Ssid == rede).OrderByDescending(r => r.SignalBars).FirstOrDefault();
            if (alvo is null) return new ResultadoConexao(false, "A rede não está ao alcance.");
            var resultado = senha is null
                ? await _adaptador.ConnectAsync(alvo, WiFiReconnectionKind.Automatic)
                : await _adaptador.ConnectAsync(alvo, WiFiReconnectionKind.Automatic, new PasswordCredential { Password = senha });
            _ = LerAsync();
            return resultado.ConnectionStatus switch
            {
                WiFiConnectionStatus.Success => new ResultadoConexao(true, null),
                WiFiConnectionStatus.InvalidCredential => new ResultadoConexao(false, "Senha incorreta."),
                WiFiConnectionStatus.NetworkNotAvailable => new ResultadoConexao(false, "A rede não está ao alcance."),
                WiFiConnectionStatus.Timeout => new ResultadoConexao(false, "Tempo esgotado."),
                _ => new ResultadoConexao(false, "Não foi possível conectar."),
            };
        }
        catch (Exception e)
        {
            LogBarra.Erro("Wifi.Conectar", e);
            return new ResultadoConexao(false, "Não foi possível conectar.");
        }
    }

    public void Desconectar()
    {
        try
        {
            _adaptador?.Disconnect();
            _ = LerAsync();
        }
        catch (Exception e) { LogBarra.Erro("Wifi.Desconectar", e); }
    }

    public async Task LigarAsync(bool ligado)
    {
        try
        {
            if (_radio is not null) await _radio.SetStateAsync(ligado ? RadioState.On : RadioState.Off);
        }
        catch (Exception e) { LogBarra.Erro("Wifi.Ligar", e); }
        await LerAsync();
    }

    public void AbrirConfiguracoes(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception e) { LogBarra.Erro("Wifi.Configuracoes", e); }
    }

    public void Dispose()
    {
        _descartada = true;
        try
        {
            if (_adaptador is not null) _adaptador.AvailableNetworksChanged -= AoMudarRedes;
            if (_radio is not null) _radio.StateChanged -= AoMudarRadio;
            NetworkInformation.NetworkStatusChanged -= AoMudarRede;
        }
        catch (Exception e) { LogBarra.Erro("Wifi.Dispose", e); }
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui.Barra;

/// Painel de Wi-Fi (spec v2 §3.3 e §6): chave, redes por perto (conectada primeiro, depois por sinal),
/// senha para rede nova, mensagens de erro e o aviso de localização.
public sealed partial class PainelWifiViewModel : ObservableObject, IDisposable
{
    public const string ConfiguracoesDeRede = "ms-settings:network-wifi";
    public const string ConfiguracoesDeLocalizacao = "ms-settings:privacy-location";

    private readonly IFonteWifi _fonte;
    private bool _lendo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemAdaptador))]
    private bool _temAdaptador;

    public bool SemAdaptador => !TemAdaptador;
    [ObservableProperty] private bool _ligado;
    [ObservableProperty] private bool _avisoLocalizacao;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private RedeWifi? _pedindoSenha;
    [ObservableProperty] private string _senha = "";

    public ObservableCollection<RedeWifi> Redes { get; } = [];

    public PainelWifiViewModel(IFonteWifi fonte)
    {
        _fonte = fonte;
        _fonte.Mudou += Ler;
        Ler();
    }

    /// Ao abrir o painel: procura as redes de novo.
    public Task AbrirAsync()
    {
        Mensagem = null;
        PedindoSenha = null;
        Senha = "";
        return _fonte.AtualizarAsync();
    }

    partial void OnLigadoChanged(bool value)
    {
        if (!_lendo) _ = _fonte.LigarAsync(value);
    }

    [RelayCommand]
    private async Task Escolher(RedeWifi? rede)
    {
        if (rede is null || Ocupado) return;
        Mensagem = null;
        if (rede.Conectada) { _fonte.Desconectar(); return; }
        if (rede.Corporativa) { _fonte.AbrirConfiguracoes(ConfiguracoesDeRede); return; }
        if (rede.Protegida && !rede.Salva)
        {
            PedindoSenha = rede;
            Senha = "";
            return;
        }
        PedindoSenha = null;
        await Conectar(rede.Nome, null);
    }

    [RelayCommand]
    private async Task ConectarComSenha()
    {
        if (PedindoSenha is not { } rede || Ocupado) return;
        string senha = Senha;
        Senha = "";
        if (await Conectar(rede.Nome, senha)) PedindoSenha = null;
    }

    private async Task<bool> Conectar(string rede, string? senha)
    {
        Ocupado = true;
        try
        {
            var r = await _fonte.ConectarAsync(rede, senha);
            Mensagem = r.Ok ? null : r.Mensagem ?? "Não foi possível conectar.";
            return r.Ok;
        }
        finally { Ocupado = false; }
    }

    [RelayCommand] private void AbrirMais() => _fonte.AbrirConfiguracoes(ConfiguracoesDeRede);
    [RelayCommand] private void AbrirLocalizacao() => _fonte.AbrirConfiguracoes(ConfiguracoesDeLocalizacao);

    private void Ler()
    {
        _lendo = true;
        try
        {
            var e = _fonte.Atual;
            TemAdaptador = e?.TemAdaptador ?? false;
            Ligado = e?.RadioLigado ?? false;
            AvisoLocalizacao = e is { TemAdaptador: true, PermissaoLocalizacao: false };
            Redes.Clear();
            foreach (var r in (e?.Redes ?? []).OrderByDescending(r => r.Conectada).ThenByDescending(r => r.Sinal)
                         .ThenBy(r => r.Nome, StringComparer.CurrentCultureIgnoreCase))
                Redes.Add(r);
        }
        finally { _lendo = false; }
    }

    public void Dispose() => _fonte.Mudou -= Ler;
}

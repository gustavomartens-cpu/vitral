using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vitral.Core.Perfis;

namespace Vitral.Ui.Barra;

/// Ilha de status: CPU e MEM (se ligados no perfil), rede, volume e bateria (só com bateria).
/// Dado indisponível vira "—"; nada aqui lança por causa de uma fonte.
public sealed partial class IlhaStatusViewModel : ObservableObject, IDisposable
{
    public const string SemDado = "—";
    private const int PassoVolume = 2;

    private readonly IFonteDesempenho _fonteDesempenho;
    private readonly IFonteVolume _fonteVolume;
    private readonly IFonteRede _fonteRede;
    private readonly IFonteBateria _fonteBateria;
    private readonly IDisposable _tique;

    [ObservableProperty] private string _cpu = SemDado;
    [ObservableProperty] private double _cpuFracao;
    [ObservableProperty] private string _memoria = SemDado;
    [ObservableProperty] private double _memoriaFracao;
    [ObservableProperty] private string _volume = SemDado; // "52" ou "—"
    [ObservableProperty] private bool _mudo;
    [ObservableProperty] private TipoRede? _rede;
    [ObservableProperty] private string _bateria = SemDado;
    [ObservableProperty] private bool _bateriaVisivel;
    /// Desenho da bateria (Segoe Fluent Icons): um por décimo da carga, com raio quando carregando.
    [ObservableProperty] private string _iconeBateria = "";

    public bool MostrarDesempenho { get; }

    public IlhaStatusViewModel(IFonteDesempenho desempenho, IFonteVolume volume, IFonteRede rede, IFonteBateria bateria,
        IRelogio relogio, IlhasBarra ilhas)
    {
        _fonteDesempenho = desempenho;
        _fonteVolume = volume;
        _fonteRede = rede;
        _fonteBateria = bateria;
        MostrarDesempenho = ilhas.Desempenho;

        _fonteVolume.Mudou += AtualizarVolume;
        _fonteRede.Mudou += AtualizarRede;
        _fonteBateria.Mudou += AtualizarBateria;
        AtualizarVolume();
        AtualizarRede();
        AtualizarBateria();
        AtualizarDesempenho();
        _tique = relogio.ACadaSegundo(AtualizarDesempenho);
    }

    /// Roda do mouse: ±2 por passo, entre 0 e 100.
    public void Rolar(int passos)
    {
        if (_fonteVolume.Volume is not { } atual) return;
        _fonteVolume.Definir(Math.Clamp(atual + passos * PassoVolume, 0, 100));
    }

    [RelayCommand]
    private void AlternarMudo()
    {
        if (_fonteVolume.Mudo is null) return;
        _fonteVolume.AlternarMudo();
    }

    /// Relê tudo (por exemplo ao retomar da suspensão).
    public void Reler()
    {
        AtualizarVolume();
        AtualizarRede();
        AtualizarBateria();
        AtualizarDesempenho();
    }

    private void AtualizarDesempenho()
    {
        var d = _fonteDesempenho.Ler();
        Cpu = d is null ? SemDado : Porcento(d.Cpu);
        CpuFracao = d?.Cpu ?? 0;
        Memoria = d is null ? SemDado : Porcento(d.Memoria);
        MemoriaFracao = d?.Memoria ?? 0;
    }

    private void AtualizarVolume()
    {
        Volume = _fonteVolume.Volume?.ToString() ?? SemDado;
        Mudo = _fonteVolume.Mudo ?? false;
    }

    private void AtualizarRede() => Rede = _fonteRede.Atual;

    private void AtualizarBateria()
    {
        var b = _fonteBateria.Atual;
        BateriaVisivel = b is not null;
        Bateria = b is null ? SemDado : $"{b.Porcentagem}%";
        if (b is not null) IconeBateria = IconeDaBateria(b.Porcentagem, b.Carregando);
    }

    private static string IconeDaBateria(int porcentagem, bool carregando)
    {
        int decimos = Math.Clamp(porcentagem, 0, 100) / 10;
        if (decimos == 10) return carregando ? "" : "";
        return ((char)((carregando ? 0xE85A : 0xE850) + decimos)).ToString();
    }

    private static string Porcento(double fracao) => $"{Math.Round(fracao * 100)}%";

    public void Dispose()
    {
        _tique.Dispose();
        _fonteVolume.Mudou -= AtualizarVolume;
        _fonteRede.Mudou -= AtualizarRede;
        _fonteBateria.Mudou -= AtualizarBateria;
    }
}

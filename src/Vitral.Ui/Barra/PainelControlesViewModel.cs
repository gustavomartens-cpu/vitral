using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vitral.Core.Controles;

namespace Vitral.Ui.Barra;

/// O painel de controles que desce da ilha de status (spec dos controles §3): interruptores, brilho e os aparelhos
/// Bluetooth. Relê o estado ao abrir e depois de cada clique (acompanha o que mudou fora do Vitral).
public sealed partial class PainelControlesViewModel(IFonteControles fonte, Func<DateTime> agora) : ObservableObject
{
    public const string TextoLuzDesconhecida = "Abra a luz noturna nas Configurações do Windows";
    private static readonly TimeSpan IntervaloBrilho = TimeSpan.FromMilliseconds(100);

    private readonly Dictionary<string, DateTime> _ultimaEscrita = [];
    private readonly Dictionary<string, int> _brilhoPendente = [];

    [ObservableProperty] private bool? _bluetooth;
    [ObservableProperty] private bool _aviao;
    [ObservableProperty] private bool? _naoPerturbe;
    [ObservableProperty] private bool? _luzNoturna;
    [ObservableProperty] private string? _textoLuzNoturna;
    [ObservableProperty] private bool _avisoTeclado;
    [ObservableProperty] private bool _mostrarAparelhos;

    public ObservableCollection<TelaBrilho> Telas { get; } = [];
    public ObservableCollection<AparelhoBluetooth> Aparelhos { get; } = [];

    /// O "Não perturbe" lido ou mudado pelo painel: o notch para (ou volta) de descer com as notificações dos apps.
    public event Action<bool>? MudouNaoPerturbe;

    private int _geracao;

    /// Relê o estado. Uma leitura que termina depois de outra mais nova é descartada (a abertura do painel e a releitura
    /// de um clique podem rodar juntas, e a leitura é lenta).
    public async Task Abrir()
    {
        int minha = ++_geracao;
        var e = await fonte.Ler();
        if (minha != _geracao) return;
        _brilhoPendente.Clear(); // era de uma tela que pode ter sumido ou mudado
        Bluetooth = e.Bluetooth;
        Aviao = e.Aviao;
        NaoPerturbe = e.NaoPerturbe;
        if (e.NaoPerturbe is { } np) MudouNaoPerturbe?.Invoke(np); // pode ter mudado fora do Vitral (Win+A)
        LuzNoturna = e.LuzNoturna;
        TextoLuzNoturna = e.LuzNoturna is null ? TextoLuzDesconhecida : null;
        // As mesmas telas: os deslizantes ficam (recriar no meio de um arrasto solta o deslizante da mão do usuário).
        if (!Telas.Select(t => (t.Id, t.Nome)).SequenceEqual(e.Telas.Select(t => (t.Id, t.Nome)))) Trocar(Telas, e.Telas);
        Trocar(Aparelhos, e.Aparelhos);
        AvisoTeclado = RegrasControles.AvisarTeclado(e.Aparelhos);
    }

    private static void Trocar<T>(ObservableCollection<T> lista, IEnumerable<T> novos)
    {
        lista.Clear();
        foreach (var n in novos) lista.Add(n);
    }

    [RelayCommand]
    private async Task AlternarBluetooth()
    {
        if (Bluetooth is not { } ligado) return;
        await fonte.Bluetooth(!ligado);
        await Abrir();
    }

    [RelayCommand]
    private async Task AlternarAviao()
    {
        await fonte.Aviao(!Aviao);
        await Abrir();
    }

    [RelayCommand]
    private async Task AlternarNaoPerturbe()
    {
        if (NaoPerturbe is not { } ligado) return;
        if (await fonte.NaoPerturbe(!ligado)) MudouNaoPerturbe?.Invoke(!ligado);
        await Abrir();
    }

    [RelayCommand]
    private async Task AlternarLuzNoturna()
    {
        if (LuzNoturna is not { } ligada) return;
        await fonte.LuzNoturna(!ligada);
        await Abrir();
    }

    [RelayCommand]
    private void AlternarAparelhos() => MostrarAparelhos = !MostrarAparelhos;

    [RelayCommand]
    private async Task Aparelho(AparelhoBluetooth? aparelho)
    {
        if (aparelho is null) return;
        await fonte.Aparelho(aparelho);
        await Abrir();
    }

    /// O deslizante de uma tela: no máximo uma escrita a cada 100 ms por tela; a última vai no <see cref="Tique"/>.
    public void MudarBrilho(string telaId, int valor)
    {
        var t = agora();
        if (_ultimaEscrita.TryGetValue(telaId, out var ultima) && t - ultima < IntervaloBrilho)
        {
            _brilhoPendente[telaId] = valor;
            return;
        }
        Escrever(telaId, valor, t);
    }

    /// Chamado de tempos em tempos enquanto o painel está aberto: escreve o último brilho que ficou esperando.
    public void Tique()
    {
        var t = agora();
        foreach (var (tela, valor) in _brilhoPendente.ToList())
            if (!_ultimaEscrita.TryGetValue(tela, out var ultima) || t - ultima >= IntervaloBrilho) Escrever(tela, valor, t);
    }

    /// O painel fechou: o último brilho que esperava os 100 ms vai na hora (senão se perderia).
    public void Descarregar()
    {
        var t = agora();
        foreach (var (tela, valor) in _brilhoPendente.ToList()) Escrever(tela, valor, t);
    }

    private void Escrever(string telaId, int valor, DateTime t)
    {
        _brilhoPendente.Remove(telaId);
        _ultimaEscrita[telaId] = t;
        fonte.Brilho(telaId, Math.Clamp(valor, 0, 100));
    }
}

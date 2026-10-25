using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

internal sealed class MidiaFalsa : IFonteMidia
{
    private EstadoMidia? _atual;
    public EstadoMidia? Atual { get => _atual; set { _atual = value; Mudou?.Invoke(); } }
    public event Action? Mudou;
    public List<string> Comandos { get; } = [];
    public void Anterior() => Comandos.Add("anterior");
    public void TocarPausar() => Comandos.Add("tocarPausar");
    public void Proxima() => Comandos.Add("proxima");
    public void TrazerApp() => Comandos.Add("trazerApp");
}

internal sealed class VolumeFalso : IFonteVolume
{
    public int? Volume { get; set; } = 50;
    public bool? Mudo { get; set; } = false;
    public event Action? Mudou;
    public List<int> Definidos { get; } = [];
    public int MudoAlternado { get; private set; }
    public void Definir(int volume) { Definidos.Add(volume); Volume = volume; Mudou?.Invoke(); }
    public void AlternarMudo() { MudoAlternado++; Mudo = !(Mudo ?? false); Mudou?.Invoke(); }
    public void Disparar() => Mudou?.Invoke();
}

internal sealed class RedeFalsa : IFonteRede
{
    public TipoRede? Atual { get; set; } = TipoRede.Wifi;
    public event Action? Mudou;
    public void Disparar() => Mudou?.Invoke();
}

internal sealed class BateriaFalsa : IFonteBateria
{
    public EstadoBateria? Atual { get; set; }
    public event Action? Mudou;
    public void Disparar() => Mudou?.Invoke();
}

internal sealed class DesempenhoFalso : IFonteDesempenho
{
    public Desempenho? Proximo { get; set; }
    public Desempenho? Ler() => Proximo;
}

internal sealed class AgoraFalso(DateTime agora) : IRelogioDoSistema
{
    public DateTime Agora { get; set; } = agora;
}

internal sealed class DispositivosFalsos : IFonteDispositivosAudio
{
    public IReadOnlyList<DispositivoAudio> Saidas { get; set; } =
        [new("alto", "Alto-falantes", true), new("fone", "Fone Bluetooth", false)];
    public bool PodeTrocar { get; set; } = true;
    public event Action? Mudou;
    public List<string> Tornados { get; } = [];
    public void TornarPadrao(string id) => Tornados.Add(id);
    public void Disparar() => Mudou?.Invoke();
}

internal sealed class ModoEnergiaFalso : IFonteModoEnergia
{
    public ModoEnergia? Atual { get; set; } = ModoEnergia.Equilibrado;
    public event Action? Mudou;
    public List<ModoEnergia> Definidos { get; } = [];
    public int Relidas { get; private set; }
    public void Definir(ModoEnergia modo) { Definidos.Add(modo); Atual = modo; Mudou?.Invoke(); }
    public void Reler() => Relidas++;
}

internal sealed class WifiFalso : IFonteWifi
{
    public EstadoWifi? Atual { get; set; } = new(true, true, true,
    [
        new("Vizinho", 2, true, false, false, false),
        new("Casa", 4, true, true, true, false),
        new("Casa 2.4", 3, true, true, false, false),
        new("Escritorio", 4, true, false, false, true),
        new("Aberta", 1, false, false, false, false),
    ]);
    public event Action? Mudou;
    public int Atualizacoes { get; private set; }
    public List<(string Rede, string? Senha)> Conexoes { get; } = [];
    public ResultadoConexao Resposta { get; set; } = new(true, null);
    public int Desconexoes { get; private set; }
    public List<bool> Ligacoes { get; } = [];
    public List<string> Abertos { get; } = [];

    public Task AtualizarAsync() { Atualizacoes++; Mudou?.Invoke(); return Task.CompletedTask; }
    public Task<ResultadoConexao> ConectarAsync(string rede, string? senha) { Conexoes.Add((rede, senha)); 
        if (AtualizarAoConectar) { Atual = Atual! with { Redes = Atual.Redes.Select(r => r with { }).ToList() }; Mudou?.Invoke(); } // como o Windows: lista nova
        return Task.FromResult(Resposta);
    }
    public bool AtualizarAoConectar { get; set; }
    public void Desconectar() => Desconexoes++;
    public Task LigarAsync(bool ligado) { Ligacoes.Add(ligado); return Task.CompletedTask; }
    public void AbrirConfiguracoes(string uri) => Abertos.Add(uri);
}

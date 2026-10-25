namespace Vitral.Ui.Barra;

// Fontes de dados das ilhas da barra do topo. O App implementa com APIs do Windows; os testes usam falsas.
// Contrato das implementações: nunca lançam (falha vira null e log) e disparam Mudou na thread da interface.

public sealed record EstadoMidia(string? Titulo, string? Artista, string NomeApp, bool Tocando, byte[]? Capa);

public interface IFonteMidia
{
    /// Null quando nada está tocando (a ilha some).
    EstadoMidia? Atual { get; }
    event Action? Mudou;
    void Anterior();
    void TocarPausar();
    void Proxima();
    void TrazerApp();
}

public interface IFonteVolume
{
    /// 0 a 100; null quando indisponível.
    int? Volume { get; }
    bool? Mudo { get; }
    event Action? Mudou;
    void Definir(int volume);
    void AlternarMudo();
}

public enum TipoRede { Desconectada, Wifi, Cabo }

public interface IFonteRede
{
    TipoRede? Atual { get; }
    event Action? Mudou;
}

/// TempoRestante: null carregando ou quando o Windows não sabe.
public sealed record EstadoBateria(int Porcentagem, bool Carregando, TimeSpan? TempoRestante = null);

public interface IFonteBateria
{
    /// Null sem bateria (desktop) ou indisponível.
    EstadoBateria? Atual { get; }
    event Action? Mudou;
}

/// CPU e memória em uso, de 0 a 1.
public sealed record Desempenho(double Cpu, double Memoria);

public interface IFonteDesempenho
{
    /// Null na primeira leitura (a CPU precisa de duas amostras) e em falha.
    Desempenho? Ler();
}

public interface IRelogioDoSistema
{
    DateTime Agora { get; }
}

public sealed record DispositivoAudio(string Id, string Nome, bool Padrao);

/// Dispositivos de reprodução ativos e a troca do padrão (spec v2 §5). PodeTrocar = false quando a interface
/// não documentada do Windows (IPolicyConfig) não está disponível.
public interface IFonteDispositivosAudio
{
    IReadOnlyList<DispositivoAudio> Saidas { get; }
    bool PodeTrocar { get; }
    event Action? Mudou;
    void TornarPadrao(string id);
}

public enum ModoEnergia { Economia, Equilibrado, Desempenho }

/// Modo de energia do Windows 11 (spec v2 §5). Atual null = indisponível (o seletor some). Usa uma chamada não
/// documentada do Windows; Reler é chamado ao abrir o painel (o Windows não avisa a troca feita por fora).
public interface IFonteModoEnergia
{
    ModoEnergia? Atual { get; }
    event Action? Mudou;
    void Definir(ModoEnergia modo);
    void Reler();
}

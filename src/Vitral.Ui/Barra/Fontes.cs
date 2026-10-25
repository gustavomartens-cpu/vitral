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

public sealed record EstadoBateria(int Porcentagem, bool Carregando);

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

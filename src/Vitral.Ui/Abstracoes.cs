namespace Vitral.Ui;

/// Diálogos do sistema. A janela WPF implementa; os testes usam um falso.
public interface IDialogos
{
    string? EscolherArquivoParaAbrir(string filtro);
    string? EscolherArquivoParaSalvar(string filtro, string nomeSugerido);
    bool Confirmar(string pergunta);
    void Avisar(string mensagem);
    string? PedirTexto(string pergunta, string sugestao);
}

/// Relógio de 1 segundo. A janela usa um DispatcherTimer; os testes avançam à mão.
public interface IRelogio
{
    IDisposable ACadaSegundo(Action tique);
}

public enum Escolha { NaoMexer, Sim, Nao }

public enum ModoOpcional { NaoMexer, Claro, Escuro }

public enum AlinhamentoOpcional { NaoMexer, Esquerda, Centro }

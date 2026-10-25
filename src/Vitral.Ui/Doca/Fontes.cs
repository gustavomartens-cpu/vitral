using Vitral.Core.Doca;

namespace Vitral.Ui.Doca;

// Fontes da doca (spec da doca §5 e §6). O App implementa (ManagedShell, COM); os testes usam falsas.
// Contrato: nunca lançam; Mudou na thread da interface.

/// Uma janela que entra na doca. Icone é do WPF (ImageSource), opaco aqui.
public sealed record JanelaApp(IntPtr Handle, string Titulo, string? AppId, string Executavel, bool Minimizada, bool PedindoAtencao, object? Icone);

public interface IFonteJanelas
{
    IReadOnlyList<JanelaApp> Janelas { get; }
    IntPtr Ativa { get; }
    event Action? Mudou;
    void Ativar(IntPtr janela);
    void Minimizar(IntPtr janela);
    void Fechar(IntPtr janela);
}

public interface IFonteFixados
{
    IReadOnlyList<Fixado> Lista { get; }
    bool Existe(Fixado fixado);
    /// Ícone do atalho, para o fixado sem janela aberta (ImageSource no App, opaco aqui).
    object? Icone(Fixado fixado);
    void Salvar(IReadOnlyList<Fixado> lista);
}

public interface IAtalhosDoca
{
    void Iniciar();
    void Pesquisar();
    void Abrir(Fixado fixado);
    void NovaJanela(string executavel);
}

/// Sem fixados (até a Task 5 trazer a lista real).
public sealed class FixadosVazios : IFonteFixados
{
    public IReadOnlyList<Fixado> Lista => [];
    public bool Existe(Fixado fixado) => true;
    public object? Icone(Fixado fixado) => null;
    public void Salvar(IReadOnlyList<Fixado> lista) { }
}

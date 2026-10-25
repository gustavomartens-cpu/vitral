namespace Vitral.Ui;

/// Uma opção de seletor em pílula: o texto mostrado e o valor que ele representa.
public sealed record Opcao<T>(string Rotulo, T Valor);

/// Opções dos seletores de três estados do editor ("Não mexer" sempre primeiro).
public static class Opcoes
{
    public static IReadOnlyList<Opcao<ModoOpcional>> Modos { get; } =
        [new("Não mexer", ModoOpcional.NaoMexer), new("Claro", ModoOpcional.Claro), new("Escuro", ModoOpcional.Escuro)];

    public static IReadOnlyList<Opcao<Escolha>> Escolhas { get; } =
        [new("Não mexer", Escolha.NaoMexer), new("Sim", Escolha.Sim), new("Não", Escolha.Nao)];

    public static IReadOnlyList<Opcao<Escolha>> Ligada { get; } =
        [new("Não mexer", Escolha.NaoMexer), new("Ligada", Escolha.Sim), new("Desligada", Escolha.Nao)];

    public static IReadOnlyList<Opcao<AlinhamentoOpcional>> Alinhamentos { get; } =
        [new("Não mexer", AlinhamentoOpcional.NaoMexer), new("Centro", AlinhamentoOpcional.Centro), new("Esquerda", AlinhamentoOpcional.Esquerda)];
}

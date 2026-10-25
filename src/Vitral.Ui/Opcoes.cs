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

    /// Modo de energia do painel de bateria (sem "Não mexer": é uma ação direta, não um ajuste de perfil).
    public static IReadOnlyList<Opcao<Barra.ModoEnergia>> ModosEnergia { get; } =
        [new("Economia", Barra.ModoEnergia.Economia), new("Equilibrado", Barra.ModoEnergia.Equilibrado), new("Desempenho", Barra.ModoEnergia.Desempenho)];

    public static IReadOnlyList<Opcao<PesquisaOpcional>> Pesquisas { get; } =
        [new("Não mexer", PesquisaOpcional.NaoMexer), new("Oculta", PesquisaOpcional.Oculta), new("Ícone", PesquisaOpcional.Icone), new("Caixa", PesquisaOpcional.Caixa)];

    public static IReadOnlyList<Opcao<AgruparOpcional>> Agrupamentos { get; } =
        [new("Não mexer", AgruparOpcional.NaoMexer), new("Sempre", AgruparOpcional.Sempre), new("Quando cheia", AgruparOpcional.QuandoCheia), new("Nunca", AgruparOpcional.Nunca)];

    /// Ícones perto do relógio: "Escolher" liga a lista (Sim).
    public static IReadOnlyList<Opcao<Escolha>> EscolherIcones { get; } =
        [new("Não mexer", Escolha.NaoMexer), new("Escolher", Escolha.Sim)];

    public static IReadOnlyList<Opcao<AlinhamentoOpcional>> Alinhamentos { get; } =
        [new("Não mexer", AlinhamentoOpcional.NaoMexer), new("Centro", AlinhamentoOpcional.Centro), new("Esquerda", AlinhamentoOpcional.Esquerda)];
}

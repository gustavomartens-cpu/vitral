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

    /// Aba Aparência: o "Não mexer" / "do tema" dos campos anuláveis. Um item nulo nunca aparece selecionado numa
    /// lista, então ele vale esta marca na tela e vira nulo no editor (conversor NuloComoMarca do App).
    public static object Nulo { get; } = new();

    public static IReadOnlyList<Opcao<object>> TemasAparencia { get; } =
    [
        new("Não mexer", Nulo), new("Automático", Core.Perfis.TemaAparencia.Automatico),
        new("Vidro escuro", Core.Perfis.TemaAparencia.VidroEscuro), new("Preto sólido", Core.Perfis.TemaAparencia.PretoSolido),
        new("Claro", Core.Perfis.TemaAparencia.Claro), new("Vidro colorido", Core.Perfis.TemaAparencia.VidroColorido),
        new("Grafite", Core.Perfis.TemaAparencia.Grafite), new("Cristal", Core.Perfis.TemaAparencia.Cristal),
    ];

    public static IReadOnlyList<Opcao<object>> MateriaisAparencia { get; } =
        [new("Do tema", Nulo), new("Vidro", Core.Perfis.MaterialParte.Vidro), new("Sólido", Core.Perfis.MaterialParte.Solido)];

    public static IReadOnlyList<Opcao<object>> CantosAparencia { get; } =
        [new("Do tema", Nulo), new("Retos", Core.Perfis.CantosParte.Retos), new("Suaves", Core.Perfis.CantosParte.Suaves), new("Redondos", Core.Perfis.CantosParte.Redondos)];

    public static IReadOnlyList<Opcao<object>> FundosAparencia { get; } =
    [
        new("do tema", Nulo), new("Azul-noite", "#0A0F20"), new("Grafite", "#24262B"), new("Preto", "#0B0C10"),
        new("Violeta", "#783CB4"), new("Verde-mar", "#1F6F5C"), new("Vinho", "#7A2E2E"), new("Claro", "#F4F4F6"),
    ];

    // Aba Formas (spec das formas §5.4): "Não mexer" primeiro, como marca Nulo.
    public static IReadOnlyList<Opcao<object>> FormatosDoca { get; } =
        [new("Não mexer", Nulo), new("Flutuante", Core.Perfis.FormatoDoca.Flutuante), new("Corrida", Core.Perfis.FormatoDoca.Corrida)];

    public static IReadOnlyList<Opcao<object>> IconesDoca { get; } =
    [
        new("Não mexer", Nulo), new("Pequenos", Core.Perfis.TamanhoIcones.Pequeno), new("Médios", Core.Perfis.TamanhoIcones.Medio),
        new("Grandes", Core.Perfis.TamanhoIcones.Grande),
    ];

    public static IReadOnlyList<Opcao<object>> Ampliacoes { get; } = [new("Não mexer", Nulo), new("Ligada", true), new("Desligada", false)];

    public static IReadOnlyList<Opcao<object>> FormatosNotch { get; } =
    [
        new("Não mexer", Nulo), new("Encaixado", Core.Perfis.FormatoNotch.Encaixado), new("Sem orelhas", Core.Perfis.FormatoNotch.SemOrelhas),
        new("Ilha", Core.Perfis.FormatoNotch.Ilha),
    ];

    public static IReadOnlyList<Opcao<object>> TamanhosNotch { get; } =
    [
        new("Não mexer", Nulo), new("Compacto", Core.Perfis.TamanhoNotch.Compacto), new("Normal", Core.Perfis.TamanhoNotch.Normal),
        new("Grande", Core.Perfis.TamanhoNotch.Grande),
    ];

    public static IReadOnlyList<Opcao<object>> DisposicoesIlhas { get; } =
        [new("Não mexer", Nulo), new("Separadas", Core.Perfis.DisposicaoIlhas.Separadas), new("Barra única", Core.Perfis.DisposicaoIlhas.BarraUnica)];

    public static IReadOnlyList<Opcao<object>> DistanciasIlhas { get; } =
        [new("Não mexer", Nulo), new("Com respiro", Core.Perfis.DistanciaIlhas.ComRespiro), new("Encostadas", Core.Perfis.DistanciaIlhas.Encostadas)];

    public static IReadOnlyList<Opcao<object>> DestaquesAparencia { get; } =
    [
        new("do tema", Nulo), new("Âmbar", "#F2B544"), new("Azul", "#4CC2FF"), new("Verde-água", "#3DDBB5"),
        new("Coral", "#FF8A8A"), new("Branco", "#FFFFFF"), new("Terracota", "#D97757"),
    ];
}

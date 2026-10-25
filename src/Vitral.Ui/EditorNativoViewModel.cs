using CommunityToolkit.Mvvm.ComponentModel;
using Vitral.Core.Perfis;

namespace Vitral.Ui;

/// Editor dos ajustes nativos. Cada opção tem "Não mexer", e "Não mexer" vira null no perfil (nunca false).
public sealed partial class EditorNativoViewModel : ObservableObject
{
    [ObservableProperty] private ModoOpcional _modoSistema;
    [ObservableProperty] private ModoOpcional _modoApps;
    [ObservableProperty] private string? _corDestaque;
    [ObservableProperty] private Escolha _corAutomatica;
    [ObservableProperty] private Escolha _transparencia;
    [ObservableProperty] private Escolha _destaqueNaBarraEIniciar;
    [ObservableProperty] private Escolha _destaqueNosTitulos;
    [ObservableProperty] private AlinhamentoOpcional _alinhamento;

    public IReadOnlyList<string> CoresSugeridas { get; } =
    [
        "#0078D4", "#21375E", "#0063B1", "#8764B8", "#744DA9", "#E3008C", "#C30052", "#E81123", "#CA5010",
        "#FF8C00", "#FFB900", "#107C10", "#00B294", "#038387", "#2D7D9A", "#69797E", "#567C73", "#4C4A48",
    ];

    public void Carregar(AjustesNativosPerfil? a)
    {
        ModoSistema = Para(a?.ModoSistema);
        ModoApps = Para(a?.ModoApps);
        CorDestaque = a?.CorDestaque;
        CorAutomatica = Para(a?.CorDestaqueAutomatica);
        Transparencia = Para(a?.Transparencia);
        DestaqueNaBarraEIniciar = Para(a?.DestaqueNaBarraEIniciar);
        DestaqueNosTitulos = Para(a?.DestaqueNosTitulos);
        Alinhamento = a?.AlinhamentoBarra switch
        {
            Core.Perfis.Alinhamento.Esquerda => AlinhamentoOpcional.Esquerda,
            Core.Perfis.Alinhamento.Centro => AlinhamentoOpcional.Centro,
            _ => AlinhamentoOpcional.NaoMexer,
        };
    }

    public AjustesNativosPerfil ParaPerfil() => new(
        De(ModoSistema),
        De(ModoApps),
        string.IsNullOrWhiteSpace(CorDestaque) ? null : CorDestaque.Trim(),
        De(CorAutomatica),
        De(Transparencia),
        De(DestaqueNaBarraEIniciar),
        De(DestaqueNosTitulos),
        Alinhamento switch
        {
            AlinhamentoOpcional.Esquerda => Core.Perfis.Alinhamento.Esquerda,
            AlinhamentoOpcional.Centro => Core.Perfis.Alinhamento.Centro,
            _ => null,
        });

    private static Escolha Para(bool? b) => b switch { true => Escolha.Sim, false => Escolha.Nao, null => Escolha.NaoMexer };
    private static bool? De(Escolha e) => e switch { Escolha.Sim => true, Escolha.Nao => false, _ => null };
    private static ModoOpcional Para(Modo? m) => m switch { Modo.Claro => ModoOpcional.Claro, Modo.Escuro => ModoOpcional.Escuro, _ => ModoOpcional.NaoMexer };
    private static Modo? De(ModoOpcional m) => m switch { ModoOpcional.Claro => Modo.Claro, ModoOpcional.Escuro => Modo.Escuro, _ => null };
}

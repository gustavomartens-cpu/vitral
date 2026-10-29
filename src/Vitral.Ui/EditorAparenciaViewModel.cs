using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.Ui;

/// Os campos do "personalizar" de uma parte (ou do geral). Nulo = "do tema". <see cref="Diferenciar"/> só vale para
/// doca, notch e ilhas: desmarcado, a parte segue o geral.
public sealed partial class EditorAjustesAparencia : ObservableObject
{
    [ObservableProperty] private MaterialParte? _material;
    [ObservableProperty] private string? _corFundo;
    [ObservableProperty] private int? _transparencia;
    [ObservableProperty] private string? _corDestaque;
    [ObservableProperty] private CantosParte? _cantos;
    [ObservableProperty] private bool _diferenciar;

    public void Carregar(AjustesAparencia? a)
    {
        Material = a?.Material;
        CorFundo = a?.CorFundo;
        Transparencia = a?.Transparencia;
        CorDestaque = a?.CorDestaque;
        Cantos = a?.Cantos;
        Diferenciar = a is not null;
    }

    /// Nulo quando nada foi escolhido.
    public AjustesAparencia? ParaPerfil() =>
        Material is null && CorFundo is null && Transparencia is null && CorDestaque is null && Cantos is null
            ? null : new AjustesAparencia(Material, CorFundo, Transparencia, CorDestaque, Cantos);
}

/// Editor da seção aparencia do perfil (spec da aparência §5.4): o tema ("Não mexer" = nulo), o personalizar geral e o
/// diferenciar por parte, com a prévia calculada pela mesma regra da tela.
public sealed partial class EditorAparenciaViewModel : ObservableObject
{
    [ObservableProperty] private TemaAparencia? _tema;
    [ObservableProperty] private ValoresAparencia? _previa;

    public EditorAparenciaViewModel()
    {
        foreach (var e in new[] { Geral, Doca, Notch, Ilhas }) e.PropertyChanged += AoMudarCampo;
    }

    /// O visual do Windows agora (para o Automático e a transparência); o App liga o leitor real.
    public Func<VisualWindows> LerWindows { get; set; } = () => new VisualWindows(true, "#0078D4", true);

    /// "Não mexer" (nulo) e os sete temas, na ordem da grade.
    public IReadOnlyList<TemaAparencia?> Temas { get; } = [null, .. Enum.GetValues<TemaAparencia>().Cast<TemaAparencia?>()];

    public EditorAjustesAparencia Geral { get; } = new();
    public EditorAjustesAparencia Doca { get; } = new();
    public EditorAjustesAparencia Notch { get; } = new();
    public EditorAjustesAparencia Ilhas { get; } = new();

    public void Carregar(AparenciaPerfil? a)
    {
        Tema = a?.Tema;
        Geral.Carregar(a?.Geral);
        Doca.Carregar(a?.Doca);
        Notch.Carregar(a?.Notch);
        Ilhas.Carregar(a?.Ilhas);
        Recalcular();
    }

    public AparenciaPerfil? ParaPerfil() => Tema is not { } tema ? null
        : new AparenciaPerfil(tema, Geral.ParaPerfil(), Parte(Doca), Parte(Notch), Parte(Ilhas));

    private static AjustesAparencia? Parte(EditorAjustesAparencia e) => e.Diferenciar ? e.ParaPerfil() : null;

    partial void OnTemaChanged(TemaAparencia? value) => Recalcular();

    /// Repassa a mudança para quem acompanha o editor (o "Alterações não salvas"), mesmo quando a prévia não muda.
    private void AoMudarCampo(object? s, PropertyChangedEventArgs e)
    {
        Recalcular();
        OnPropertyChanged(e.PropertyName);
    }

    /// A prévia usa a mesma regra que a tela; sem tema ("Não mexer"), não há prévia.
    public void Recalcular() => Previa = ParaPerfil() is { } a ? ResolverAparencia.Resolver(a, LerWindows()) : null;
}

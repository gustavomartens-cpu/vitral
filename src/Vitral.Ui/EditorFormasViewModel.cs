using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vitral.Core.Formas;
using Vitral.Core.Perfis;

namespace Vitral.Ui;

/// Editor da seção formas do perfil (spec das formas §5.4): cada campo nulo é "Não mexer"; a prévia usa a mesma regra
/// que a tela.
public sealed partial class EditorFormasViewModel : ObservableObject
{
    [ObservableProperty] private FormatoDoca? _formatoDoca;
    [ObservableProperty] private TamanhoIcones? _iconesDoca;
    [ObservableProperty] private bool? _ampliacaoDoca;
    [ObservableProperty] private FormatoNotch? _formatoNotch;
    [ObservableProperty] private TamanhoNotch? _tamanhoNotch;
    [ObservableProperty] private DisposicaoIlhas? _disposicaoIlhas;
    [ObservableProperty] private DistanciaIlhas? _distanciaIlhas;
    [ObservableProperty] private ValoresFormas _previa = ResolverFormas.Resolver(null, CantosParte.Redondos);

    /// As formas em uso agora (o App liga o estado real): "Não mexer" mantém o que está, então a prévia mostra isso
    /// nos campos não escolhidos.
    public Func<FormasPerfil> Atuais { get; set; } = () => new FormasPerfil();

    public void Carregar(FormasPerfil? f)
    {
        FormatoDoca = f?.Doca?.Formato;
        IconesDoca = f?.Doca?.Icones;
        AmpliacaoDoca = f?.Doca?.Ampliacao;
        FormatoNotch = f?.Notch?.Formato;
        TamanhoNotch = f?.Notch?.Tamanho;
        DisposicaoIlhas = f?.Ilhas?.Disposicao;
        DistanciaIlhas = f?.Ilhas?.Distancia;
    }

    /// Nulo quando nada foi escolhido; cada parte, nula quando nada dela foi escolhido.
    public FormasPerfil? ParaPerfil()
    {
        var doca = FormatoDoca is null && IconesDoca is null && AmpliacaoDoca is null ? null : new FormasDoca(FormatoDoca, IconesDoca, AmpliacaoDoca);
        var notch = FormatoNotch is null && TamanhoNotch is null ? null : new FormasNotch(FormatoNotch, TamanhoNotch);
        var ilhas = DisposicaoIlhas is null && DistanciaIlhas is null ? null : new FormasIlhas(DisposicaoIlhas, DistanciaIlhas);
        return doca is null && notch is null && ilhas is null ? null : new FormasPerfil(doca, notch, ilhas);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(Previa)) Recalcular();
    }

    public void Recalcular() => Previa = ResolverFormas.Resolver(ResolverFormas.Mesclar(Atuais(), ParaPerfil()), CantosParte.Redondos);
}

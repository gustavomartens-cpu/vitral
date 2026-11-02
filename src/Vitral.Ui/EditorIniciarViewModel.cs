using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vitral.Core.Iniciar;

namespace Vitral.Ui;

/// O Iniciar na aba Aparência (spec do Iniciar §4.3): o modo do perfil (nulo = "Não mexer") e a linha de estado do que
/// está em vigor, com o botão da reserva quando o componente não conectou.
public sealed partial class EditorIniciarViewModel : ObservableObject
{
    [ObservableProperty] private ModoIniciar? _modo;

    /// O estado em vigor (o App liga o ServicoIniciar); nulo = modo Windows, sem linha.
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TextoEstado), nameof(MostrarReserva))]
    private MotivoIniciar? _estado;

    public string? TextoEstado => Estado is { } m ? RegrasIniciar.Texto(m) : null;

    public bool MostrarReserva => Estado is { } m && m != MotivoIniciar.Nenhum;

    /// O botão "Usar a cor de destaque do Windows no Iniciar".
    public event Action? PediuDestaqueDoWindows;

    [RelayCommand]
    private void UsarDestaque() => PediuDestaqueDoWindows?.Invoke();

    public void Carregar(IniciarPerfil? iniciar) => Modo = iniciar?.Modo;

    public IniciarPerfil? ParaPerfil() => Modo is { } modo ? new IniciarPerfil(modo) : null;
}

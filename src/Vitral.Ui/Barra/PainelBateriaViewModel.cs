using CommunityToolkit.Mvvm.ComponentModel;

namespace Vitral.Ui.Barra;

/// Painel de bateria e energia (spec v2 §3.3): porcentagem, tempo restante ou estado da carga, e o modo de energia.
public sealed partial class PainelBateriaViewModel : ObservableObject, IDisposable
{
    private readonly IFonteBateria _fonteBateria;
    private readonly IFonteModoEnergia _fonteModo;
    private bool _lendo;

    [ObservableProperty] private string _porcentagem = IlhaStatusViewModel.SemDado;
    [ObservableProperty] private double _fracao;
    [ObservableProperty] private string _detalhe = "";
    [ObservableProperty] private ModoEnergia? _modo;
    [ObservableProperty] private bool _mostrarModo;

    public PainelBateriaViewModel(IFonteBateria bateria, IFonteModoEnergia modo)
    {
        _fonteBateria = bateria;
        _fonteModo = modo;
        _fonteBateria.Mudou += LerBateria;
        _fonteModo.Mudou += LerModo;
        LerBateria();
        LerModo();
    }

    /// Ao abrir o painel: relê o modo (pode ter sido trocado pelas configurações do Windows).
    public void Abrir()
    {
        _fonteModo.Reler();
        LerModo();
    }

    partial void OnModoChanged(ModoEnergia? value)
    {
        if (!_lendo && value is { } modo) _fonteModo.Definir(modo);
    }

    private void LerBateria()
    {
        var b = _fonteBateria.Atual;
        Porcentagem = b is null ? IlhaStatusViewModel.SemDado : $"{b.Porcentagem}%";
        Fracao = (b?.Porcentagem ?? 0) / 100.0;
        Detalhe = b switch
        {
            null => "",
            { Carregando: true, Porcentagem: >= 100 } => "Conectado",
            { Carregando: true } => "Carregando",
            { TempoRestante: { } t } => Tempo(t),
            _ => "Usando a bateria",
        };
    }

    private void LerModo()
    {
        _lendo = true;
        try
        {
            Modo = _fonteModo.Atual;
            MostrarModo = _fonteModo.Atual is not null;
        }
        finally { _lendo = false; }
    }

    private static string Tempo(TimeSpan t)
    {
        int horas = (int)t.TotalHours, minutos = t.Minutes;
        if (horas == 0) return $"{minutos} min";
        return minutos == 0 ? $"{horas} h" : $"{horas} h {minutos} min";
    }

    public void Dispose()
    {
        _fonteBateria.Mudou -= LerBateria;
        _fonteModo.Mudou -= LerModo;
    }
}

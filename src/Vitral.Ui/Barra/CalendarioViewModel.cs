using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui.Barra;

public sealed record DiaCalendario(int Numero, bool DoMes, bool Hoje);

/// Calendário do notch: só o mês (spec v2 §3.3). Grade de 6 semanas começando no domingo.
public sealed partial class CalendarioViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IRelogioDoSistema _agora;
    private DateTime _mes;

    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _dataPorExtenso = "";
    [ObservableProperty] private IReadOnlyList<DiaCalendario> _dias = [];

    public CalendarioViewModel(IRelogioDoSistema agora)
    {
        _agora = agora;
        Reabrir();
    }

    /// Ao abrir o painel: volta ao mês atual e relê o dia de hoje.
    public void Reabrir()
    {
        var hoje = _agora.Agora.Date;
        _mes = new DateTime(hoje.Year, hoje.Month, 1);
        Montar();
    }

    [RelayCommand] private void Anterior() { _mes = _mes.AddMonths(-1); Montar(); }
    [RelayCommand] private void Proximo() { _mes = _mes.AddMonths(1); Montar(); }
    [RelayCommand] private void Hoje() => Reabrir();

    private void Montar()
    {
        var hoje = _agora.Agora.Date;
        var inicio = _mes.AddDays(-(int)_mes.DayOfWeek);
        Dias = Enumerable.Range(0, 42)
            .Select(i => inicio.AddDays(i))
            .Select(d => new DiaCalendario(d.Day, d.Month == _mes.Month, d == hoje))
            .ToList();
        Titulo = _mes.ToString("MMMM 'de' yyyy", PtBr);
        DataPorExtenso = hoje.ToString("dddd, d 'de' MMMM 'de' yyyy", PtBr);
    }
}

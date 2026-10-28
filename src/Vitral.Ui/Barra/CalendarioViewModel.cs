using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vitral.Core.Agenda;

namespace Vitral.Ui.Barra;

public sealed record DiaCalendario(int Numero, bool DoMes, bool Hoje, bool TemCompromisso = false, DateOnly Data = default,
    bool Escolhido = false);

/// Um compromisso na lista do dia. <paramref name="Horario"/>: "Dia inteiro" ou "14:30–15:30".
public sealed record ItemCompromisso(string Horario, string Titulo, string Cor, Uri? LinkReuniao, Uri? LinkEvento);

/// Calendário do notch (spec v2 §3.3; spec da agenda §5.4): grade de 6 semanas começando no domingo, com os dias que têm
/// compromisso marcados, e a lista do dia escolhido (hoje ao abrir).
public sealed partial class CalendarioViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IRelogioDoSistema _agora;
    private readonly ServicoAgenda? _agenda;
    private readonly Action? _aoMudar;
    private DateTime _mes;

    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _dataPorExtenso = "";
    [ObservableProperty] private IReadOnlyList<DiaCalendario> _dias = [];
    [ObservableProperty] private DateOnly _diaEscolhido;
    [ObservableProperty] private string _tituloDoDia = "Hoje";
    [ObservableProperty] private IReadOnlyList<ItemCompromisso> _doDia = [];
    [ObservableProperty] private string? _situacao;
    [ObservableProperty] private bool _temContas;

    /// <param name="agenda">Nulo: só o mês, como antes da agenda.</param>
    public CalendarioViewModel(IRelogioDoSistema agora, ServicoAgenda? agenda = null)
    {
        _agora = agora;
        _agenda = agenda;
        if (agenda is not null)
        {
            _aoMudar = () => { Montar(); MontarDia(); };
            agenda.Mudou += _aoMudar;
        }
        Reabrir();
    }

    private DateOnly Hoje_ => DateOnly.FromDateTime(_agora.Agora);

    /// Ao abrir o painel: volta ao mês atual e ao dia de hoje.
    public void Reabrir() => AbrirDia(Hoje_);

    /// O clique no aviso da agenda: o mês e o dia do compromisso.
    public void AbrirDia(DateOnly dia)
    {
        _mes = new DateTime(dia.Year, dia.Month, 1);
        DiaEscolhido = dia;
        Montar();
        MontarDia();
    }

    [RelayCommand] private void Anterior() { _mes = _mes.AddMonths(-1); Montar(); }
    [RelayCommand] private void Proximo() { _mes = _mes.AddMonths(1); Montar(); }
    [RelayCommand] private void Hoje() => Reabrir();

    [RelayCommand]
    private void EscolherDia(DiaCalendario dia)
    {
        if (!dia.DoMes) { AbrirDia(dia.Data); return; } // dia do mês vizinho: vai para o mês dele
        DiaEscolhido = dia.Data;
        Montar(); // a marca do dia escolhido
        MontarDia();
    }

    private void Montar()
    {
        var hoje = _agora.Agora.Date;
        var inicio = _mes.AddDays(-(int)_mes.DayOfWeek);
        var marcados = _agenda is null ? null : RegrasAgenda.DiasComCompromisso(_agenda.Compromissos, _mes.Year, _mes.Month, _agenda.Fuso);
        Dias = Enumerable.Range(0, 42)
            .Select(i => inicio.AddDays(i))
            .Select(d => new DiaCalendario(d.Day, d.Month == _mes.Month, d == hoje,
                d.Month == _mes.Month && marcados?.Contains(d.Day) == true, DateOnly.FromDateTime(d),
                _agenda is not null && DateOnly.FromDateTime(d) == DiaEscolhido))
            .ToList();
        Titulo = _mes.ToString("MMMM 'de' yyyy", PtBr);
        DataPorExtenso = hoje.ToString("dddd, d 'de' MMMM 'de' yyyy", PtBr);
    }

    private void MontarDia()
    {
        if (_agenda is null) return;
        TituloDoDia = DiaEscolhido == Hoje_ ? "Hoje" : DiaEscolhido.ToString("dddd, d 'de' MMMM", PtBr);
        var fuso = _agenda.Fuso;
        DoDia = RegrasAgenda.DoDia(_agenda.Compromissos, DiaEscolhido, fuso).Select(c => new ItemCompromisso(
            c.DiaInteiro ? "Dia inteiro" : $"{Hora(c.Inicio, fuso)}–{Hora(c.Fim, fuso)}", c.Titulo, c.Conta.Cor, c.LinkReuniao, c.LinkEvento)).ToList();
        var contas = _agenda.Contas;
        TemContas = contas.Count > 0;
        var antigas = contas.Values.Where(v => v.Estado != EstadoConta.Ok).ToList();
        Situacao = !TemContas ? "Ligue sua agenda em Contas…"
            : antigas.Count == 0 ? null
            : antigas.Min(v => v.AtualizadoEm) is { } quando ? $"atualizado às {Hora(quando, fuso)}"
            : "sem conexão com a agenda";
    }

    /// O painel foi fechado de vez: deixa de acompanhar a agenda.
    public void Dispose()
    {
        if (_agenda is not null && _aoMudar is not null) _agenda.Mudou -= _aoMudar;
    }

    private static string Hora(DateTimeOffset t, TimeZoneInfo fuso) => TimeZoneInfo.ConvertTime(t, fuso).ToString("HH:mm", PtBr);
}

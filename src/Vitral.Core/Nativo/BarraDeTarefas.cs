using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Nativo;

/// Traduz a seção barraTarefas do perfil e o relógio de baixo em valores de HKCU
/// (spec da barra de tarefas §4; todos valem na hora com o aviso "TraySettings", teste de 25/10).
public static class BarraDeTarefas
{
    public const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    public const string Search = @"Software\Microsoft\Windows\CurrentVersion\Search";
    public const string StuckRects = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
    public const string NomeRelogio = "ShowSystrayDateTimeValueName";
    private const int ByteDoEstado = 8;
    private const byte BitOcultar = 0x01;

    /// Valores fixos que o retrato fotografa (os ícones, que variam por PC, ficam em <see cref="Icones"/>).
    public static IReadOnlyList<(string Chave, string Nome, TipoValor Tipo)> Controlados { get; } =
    [
        (Search, "SearchboxTaskbarMode", TipoValor.Dword),
        (Advanced, "ShowTaskViewButton", TipoValor.Dword),
        (Advanced, "TaskbarSd", TipoValor.Dword),
        (Advanced, "TaskbarGlomLevel", TipoValor.Dword),
        (Advanced, "MMTaskbarEnabled", TipoValor.Dword),
        (Advanced, "TaskbarBadges", TipoValor.Dword),
        (Advanced, "TaskbarFlashing", TipoValor.Dword),
        (Advanced, NomeRelogio, TipoValor.Dword),
        (StuckRects, "Settings", TipoValor.Binario),
    ];

    /// Valores da seção (sem os ícones). Ocultar automaticamente altera só o bit do byte 8 de
    /// <paramref name="stuckRectsAtual"/>; se ele faltar ou for curto, o valor não entra (o Aplicador relata).
    public static IReadOnlyList<ValorRegistro> Calcular(BarraTarefasPerfil b, byte[]? stuckRectsAtual)
    {
        var valores = new List<ValorRegistro>();
        void Dword(string chave, string nome, uint dado) => valores.Add(new(chave, nome, TipoValor.Dword, dado));
        static uint Bit(bool v) => v ? 1u : 0u;

        if (b.Pesquisa is { } pesquisa)
            Dword(Search, "SearchboxTaskbarMode", pesquisa switch { PesquisaBarra.Oculta => 0, PesquisaBarra.Icone => 1, _ => 2 });
        if (b.VisaoTarefas is { } visao) Dword(Advanced, "ShowTaskViewButton", Bit(visao));
        if (b.MostrarAreaDeTrabalho is { } area) Dword(Advanced, "TaskbarSd", Bit(area));
        if (b.Agrupar is { } agrupar)
            Dword(Advanced, "TaskbarGlomLevel", agrupar switch { AgruparBotoes.Sempre => 0, AgruparBotoes.QuandoCheia => 1, _ => 2 });
        if (b.TodosOsMonitores is { } todos) Dword(Advanced, "MMTaskbarEnabled", Bit(todos));
        if (b.Selos is { } selos) Dword(Advanced, "TaskbarBadges", Bit(selos));
        if (b.Piscar is { } piscar) Dword(Advanced, "TaskbarFlashing", Bit(piscar));

        if (b.OcultarAutomaticamente is { } ocultar && OcultarAutomatico(stuckRectsAtual) is not null)
        {
            var novo = (byte[])stuckRectsAtual!.Clone();
            novo[ByteDoEstado] = (byte)(ocultar ? novo[ByteDoEstado] | BitOcultar : novo[ByteDoEstado] & ~BitOcultar);
            valores.Add(new(StuckRects, "Settings", TipoValor.Binario, novo));
        }
        return valores;
    }

    /// Relógio de baixo: some com a barra do topo ligada, volta (valor apagado) com ela desligada; nula = não mexer.
    public static ValorRegistro? Relogio(BarraTopoPerfil? topo) => topo switch
    {
        null => null,
        { Visivel: true } => new(Advanced, NomeRelogio, TipoValor.Dword, 0u),
        _ => new(Advanced, NomeRelogio, TipoValor.Dword, null),
    };

    /// O estado de "ocultar automaticamente" gravado no StuckRects3; null se o valor faltar ou for curto.
    public static bool? OcultarAutomatico(byte[]? settings) =>
        settings is { Length: > ByteDoEstado } ? (settings[ByteDoEstado] & BitOcultar) != 0 : null;

    /// Valores que, gravados, pedem o aviso "TraySettings".
    public static bool EhDaBarra(ValorRegistro v) =>
        string.Equals(v.Chave, Advanced, StringComparison.OrdinalIgnoreCase)
        || string.Equals(v.Chave, Search, StringComparison.OrdinalIgnoreCase)
        || string.Equals(v.Chave, StuckRects, StringComparison.OrdinalIgnoreCase);

    public static bool EhStuckRects(ValorRegistro v) =>
        string.Equals(v.Chave, StuckRects, StringComparison.OrdinalIgnoreCase) && string.Equals(v.Nome, "Settings", StringComparison.OrdinalIgnoreCase);
}

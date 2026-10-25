using System.Runtime.InteropServices;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Modo de energia do Windows 11 pelas chamadas de "overlay" do powrprof, não documentadas (as mesmas do seletor
/// das Configurações). GUID desconhecido ou erro: Atual null e o seletor some.
internal sealed partial class FonteModoEnergiaWindows : IFonteModoEnergia
{
    private static readonly Guid Economia = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid Desempenho = new("ded574b5-45a0-4f42-8737-46345c09c238");

    [LibraryImport("powrprof.dll")] private static partial uint PowerGetEffectiveOverlayScheme(out Guid esquema);
    [LibraryImport("powrprof.dll")] private static partial uint PowerSetActiveOverlayScheme(in Guid esquema);

    public ModoEnergia? Atual { get; private set; }
    public event Action? Mudou;

    public FonteModoEnergiaWindows() => Ler();

    public void Reler() => Ler();

    private void Ler()
    {
        try
        {
            if (PowerGetEffectiveOverlayScheme(out var esquema) != 0) { Atual = null; return; }
            Atual = esquema == Guid.Empty ? ModoEnergia.Equilibrado
                : esquema == Economia ? ModoEnergia.Economia
                : esquema == Desempenho ? ModoEnergia.Desempenho
                : null;
        }
        catch (Exception e)
        {
            LogBarra.Erro("ModoEnergia.Ler", e);
            Atual = null;
        }
    }

    public void Definir(ModoEnergia modo)
    {
        try
        {
            var esquema = modo switch { ModoEnergia.Economia => Economia, ModoEnergia.Desempenho => Desempenho, _ => Guid.Empty };
            uint erro = PowerSetActiveOverlayScheme(in esquema);
            if (erro != 0) LogBarra.Info($"PowerSetActiveOverlayScheme devolveu {erro}");
        }
        catch (Exception e) { LogBarra.Erro("ModoEnergia.Definir", e); }
        Ler();
        Mudou?.Invoke();
    }
}

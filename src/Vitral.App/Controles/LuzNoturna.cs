using Microsoft.Win32;
using Vitral.Core.Controles;

namespace Vitral.App.Controles;

/// A luz noturna do Windows (spec dos controles §4.4): o blob do CloudStore, lido e montado pelas regras do Core. Formato
/// desconhecido: nada é gravado.
internal static class LuzNoturna
{
    private const string Chave = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate";

    public static bool? Ler()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Chave);
        return k?.GetValue("Data") is byte[] blob ? RegrasControles.LuzNoturna(blob) : null;
    }

    public static bool Definir(bool ligar)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Chave, writable: true);
        if (k?.GetValue("Data") is not byte[] blob || RegrasControles.LuzNoturnaComo(blob, ligar) is not { } novo) return false;
        if (!ReferenceEquals(novo, blob)) k.SetValue("Data", novo, RegistryValueKind.Binary);
        return true;
    }
}

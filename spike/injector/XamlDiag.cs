using System.Runtime.InteropServices;

namespace Vitral.Spike.Injector;

/// Rota externa: o injector chama InitializeXamlDiagnosticsEx com o PID do alvo.
internal static class XamlDiag
{
    [DllImport("Windows.UI.Xaml.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int InitializeXamlDiagnosticsEx(
        string endPointName, uint pid, string? wszDllXamlDiagnostics,
        string wszTAPDllName, Guid tapClsid, string? wszInitializationData);

    /// Tenta VisualDiagConnection1..10 até S_OK. Devolve o último HRESULT.
    public static int Carregar(uint pid, string dll, Guid clsid, string dados)
    {
        int hr = -1;
        for (int n = 1; n <= 10; n++)
        {
            hr = InitializeXamlDiagnosticsEx($"VisualDiagConnection{n}", pid, null, dll, clsid, dados);
            Console.WriteLine($"  externa: VisualDiagConnection{n} → 0x{hr:X8}");
            if (hr == 0) break;
        }
        return hr;
    }
}

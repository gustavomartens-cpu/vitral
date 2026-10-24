using System.Runtime.InteropServices;

namespace Vitral.Spike.Injector;

/// Rota externa: o injector chama InitializeXamlDiagnosticsEx com o PID do alvo.
internal static class XamlDiag
{
    [DllImport("Windows.UI.Xaml.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int InitializeXamlDiagnosticsEx(
        string endPointName, uint pid, string? wszDllXamlDiagnostics,
        string wszTAPDllName, Guid tapClsid, string? wszInitializationData);

    private const int ErroNaoEncontrado = unchecked((int)0x80070490); // HRESULT_FROM_WIN32(ERROR_NOT_FOUND)

    /// O sufixo de "VisualDiagConnection<n>" é um contador interno do processo alvo e não dá para saber qual é.
    /// Como o Windhawk: tenta n = 1..10000 enquanto a resposta for ERROR_NOT_FOUND. Devolve o último HRESULT.
    public static int Carregar(uint pid, string dll, Guid clsid, string dados)
    {
        int hr = ErroNaoEncontrado;
        int n = 1;
        for (; n <= 10000 && hr == ErroNaoEncontrado; n++)
            hr = InitializeXamlDiagnosticsEx($"VisualDiagConnection{n}", pid, "", dll, clsid, dados);
        Console.WriteLine($"  externa: parou em VisualDiagConnection{n - 1} → 0x{hr:X8}");
        return hr;
    }

    /// A porta de diagnóstico só existe depois que a barra termina de montar a interface.
    /// Espera por essa condição: repete a varredura a cada 2 s enquanto nenhuma porta existir, até 90 s.
    public static int CarregarQuandoPronto(uint pid, string dll, Guid clsid, string dados)
    {
        var limite = DateTime.UtcNow.AddSeconds(90);
        int tentativa = 1;
        while (true)
        {
            Console.WriteLine($"  externa: varredura {tentativa}");
            int hr = Carregar(pid, dll, clsid, dados);
            if (hr != ErroNaoEncontrado || DateTime.UtcNow >= limite) return hr;
            Thread.Sleep(2000);
            tentativa++;
        }
    }
}

using System.Runtime.InteropServices;

namespace Vitral.App.Iniciar;

/// Rota externa da fase 0 (spec do Iniciar §4.2): o Vitral chama InitializeXamlDiagnosticsEx com o PID do Iniciar, e o
/// Windows carrega a DLL lá dentro.
internal static partial class ConexaoXaml
{
    [LibraryImport("Windows.UI.Xaml.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int InitializeXamlDiagnosticsEx(string endPointName, uint pid, string? dllXamlDiagnostics,
        string tapDll, Guid tapClsid, string? dadosIniciais);

    private const int NaoEncontrado = unchecked((int)0x80070490); // HRESULT_FROM_WIN32(ERROR_NOT_FOUND)

    /// O sufixo de "VisualDiagConnection&lt;n&gt;" é um contador do processo alvo: tenta 1..10000 enquanto não existir.
    /// A porta só aparece depois que o Iniciar monta a interface, então repete a cada 2 s até 90 s. Devolve o HRESULT.
    public static int Conectar(uint pid, string dll, Guid clsid, string dados, CancellationToken cancelar)
    {
        var limite = DateTime.UtcNow.AddSeconds(90);
        while (true)
        {
            int hr = NaoEncontrado;
            for (int n = 1; n <= 10000 && hr == NaoEncontrado; n++)
                hr = InitializeXamlDiagnosticsEx($"VisualDiagConnection{n}", pid, "", dll, clsid, dados);
            if (hr != NaoEncontrado || DateTime.UtcNow >= limite || cancelar.WaitHandle.WaitOne(2000)) return hr;
        }
    }
}

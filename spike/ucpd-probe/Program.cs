// Sonda do UCPD (fase 0, DESCARTÁVEL): regrava o MESMO valor e tipo em chaves protegidas
// para ver se o driver bloqueia a escrita feita por um exe nosso. Não cria valores ausentes.
using Microsoft.Win32;

var alvos = new (string Chave, string Valor)[]
{
    (@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa"),
    (@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode"),
    (@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl"), // controle, não protegido
};

using var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
foreach (var (chave, nome) in alvos)
{
    string resultado;
    object? valor = null;
    try
    {
        using var k = hkcu.OpenSubKey(chave, writable: true);
        valor = k?.GetValue(nome, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (k is null || valor is null) resultado = "AUSENTE";
        else
        {
            k.SetValue(nome, valor, k.GetValueKind(nome));
            resultado = "OK";
        }
    }
    catch (Exception e)
    {
        resultado = $"BLOQUEADO({e.GetType().Name}: {e.Message})";
    }
    Console.WriteLine($@"HKCU\{chave}\{nome};{valor};{resultado}");
}

using System.Runtime.InteropServices;

namespace Vitral.App.Controles;

/// O "Não perturbe" do Windows (spec dos controles §4.3) pelo COM não documentado IQuietHoursSettings, o mesmo que
/// outros programas usam (provado em 02/11): PriorityOnly = ligado, Unrestricted = desligado.
internal static class NaoPerturbe
{
    private const string Ligado = "Microsoft.QuietHoursProfile.PriorityOnly", Desligado = "Microsoft.QuietHoursProfile.Unrestricted";

    [ComImport, Guid("6BFF4732-81EC-4FFB-AE67-B6C1BC29631F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IQuietHoursSettings
    {
        [PreserveSig] int get_UserSelectedProfile([MarshalAs(UnmanagedType.LPWStr)] out string perfil);
        [PreserveSig] int put_UserSelectedProfile([MarshalAs(UnmanagedType.LPWStr)] string perfil);
    }

    private static IQuietHoursSettings? Criar()
    {
        try
        {
            return Type.GetTypeFromCLSID(new Guid("f53321fa-34f8-4b7f-b9a3-361877cb94cf")) is { } tipo
                ? Activator.CreateInstance(tipo) as IQuietHoursSettings : null;
        }
        catch (COMException) { return null; }
    }

    /// Nulo quando o Windows não tem o serviço (versão diferente) ou recusa a leitura. Nunca lança: é lido ao criar o
    /// notch, e uma falha aqui não pode impedir a barra de nascer.
    public static bool? Ler()
    {
        IQuietHoursSettings? q = null;
        try
        {
            if ((q = Criar()) is null || q.get_UserSelectedProfile(out string? perfil) != 0 || perfil is null) return null;
            return !perfil.EndsWith("Unrestricted", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Barra.LogBarra.Erro("NaoPerturbe.Ler", e);
            return null;
        }
        finally { Soltar(q); }
    }

    public static bool Definir(bool ligar)
    {
        var q = Criar();
        try { return q is not null && q.put_UserSelectedProfile(ligar ? Ligado : Desligado) == 0; }
        finally { Soltar(q); }
    }

    private static void Soltar(IQuietHoursSettings? q)
    {
        if (q is not null) Marshal.ReleaseComObject(q);
    }
}

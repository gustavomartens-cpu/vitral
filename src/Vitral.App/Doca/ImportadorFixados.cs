using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Vitral.App.Barra;
using Vitral.Core.Doca;

namespace Vitral.App.Doca;

/// Importa os fixados da barra do Windows (spec da doca §6): os .lnk da pasta "User Pinned\TaskBar", com o alvo
/// (IShellLinkW) e o AppUserModelID (IPropertyStore do atalho). Só lê; nunca lança.
internal static class ImportadorFixados
{
    public static string PastaDoWindows => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    public static IReadOnlyList<Fixado> Importar(string pasta)
    {
        var lista = new List<Fixado>();
        try
        {
            if (!Directory.Exists(pasta)) return lista;
            foreach (var lnk in Directory.EnumerateFiles(pasta, "*.lnk").Order(StringComparer.OrdinalIgnoreCase))
                if (Ler(lnk) is { } f) lista.Add(f);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("ImportadorFixados", e); }
        return lista;
    }

    private static Fixado? Ler(string lnk)
    {
        object? link = null;
        try
        {
            link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!);
            ((IPersistFile)link!).Load(lnk, 0);
            var alvo = new StringBuilder(260);
            ((IShellLinkW)link).GetPath(alvo, alvo.Capacity, IntPtr.Zero, 0);
            string? appId = LerAppId((IPropertyStore)link);
            string nome = Path.GetFileNameWithoutExtension(lnk);
            return new Fixado(lnk, appId, nome, alvo.Length > 0 ? alvo.ToString() : null);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro($"ImportadorFixados.Ler {Path.GetFileName(lnk)}", e);
            return null;
        }
        finally
        {
            if (link is not null) Marshal.ReleaseComObject(link);
        }
    }

    private static string? LerAppId(IPropertyStore loja)
    {
        var chave = new PropertyKey { Formato = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
        var valor = new PropVariant();
        try
        {
            loja.GetValue(ref chave, out valor);
            return valor.Tipo == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(valor.Ponteiro) : null;
        }
        catch (COMException) { return null; }
        finally { PropVariantClear(ref valor); }
    }

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant pv);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid Formato; public uint Id; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Tipo;
        private ushort _r1, _r2, _r3;
        public IntPtr Ponteiro;
        private IntPtr _resto;
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arquivo, int tamanho, IntPtr dados, uint flags);
        // Os demais métodos não são usados; GetPath é o primeiro da vtable.
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint quantos);
        void GetAt(uint i, out PropertyKey chave);
        void GetValue(ref PropertyKey chave, out PropVariant valor);
    }
}

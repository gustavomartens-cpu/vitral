using Microsoft.Win32;
using Vitral.Core.Registro;

namespace Vitral.Core.Windows;

/// Registro real do usuário (HKCU, visão de 64 bits). Acesso negado vira <see cref="EscritaBloqueadaException"/>.
public sealed class RegistroWindows : IRegistro
{
    private static RegistryKey Hkcu() => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

    public ValorRegistro Ler(string chave, string nome, TipoValor tipoEsperado)
    {
        using var raiz = Hkcu();
        using var k = raiz.OpenSubKey(chave);
        object? dado = k?.GetValue(nome, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (dado is null) return new ValorRegistro(chave, nome, tipoEsperado, null);
        return k!.GetValueKind(nome) switch
        {
            RegistryValueKind.DWord => new ValorRegistro(chave, nome, TipoValor.Dword, unchecked((uint)(int)dado)),
            RegistryValueKind.Binary => new ValorRegistro(chave, nome, TipoValor.Binario, (byte[])dado),
            _ => new ValorRegistro(chave, nome, TipoValor.Texto, dado.ToString()),
        };
    }

    public void Gravar(ValorRegistro v)
    {
        if (v.Ausente) { Apagar(v.Chave, v.Nome); return; }
        try
        {
            using var raiz = Hkcu();
            using var k = raiz.CreateSubKey(v.Chave, writable: true);
            switch (v.Tipo)
            {
                case TipoValor.Dword: k.SetValue(v.Nome, unchecked((int)(uint)v.Dado!), RegistryValueKind.DWord); break;
                case TipoValor.Binario: k.SetValue(v.Nome, (byte[])v.Dado!, RegistryValueKind.Binary); break;
                default: k.SetValue(v.Nome, (string)v.Dado!, RegistryValueKind.String); break;
            }
        }
        catch (UnauthorizedAccessException e) { throw new EscritaBloqueadaException(v, e); }
        catch (System.Security.SecurityException e) { throw new EscritaBloqueadaException(v, e); }
    }

    public void Apagar(string chave, string nome)
    {
        try
        {
            using var raiz = Hkcu();
            using var k = raiz.OpenSubKey(chave, writable: true);
            k?.DeleteValue(nome, throwOnMissingValue: false);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new EscritaBloqueadaException(new ValorRegistro(chave, nome, TipoValor.Dword, null), e);
        }
    }
}

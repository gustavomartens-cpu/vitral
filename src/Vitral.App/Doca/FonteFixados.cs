using System.IO;
using ManagedShell.Common.Enums;
using ManagedShell.Common.Helpers;
using Vitral.App.Barra;
using Vitral.Core.Doca;
using Vitral.Ui.Doca;

namespace Vitral.App.Doca;

/// Fixados da doca: o fixados.json do Vitral. Na primeira vez (sem arquivo), importa os da barra do Windows e grava.
internal sealed class FonteFixados(RepositorioFixados repositorio) : IFonteFixados
{
    private readonly Dictionary<string, object?> _icones = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<Fixado>? _lista;
    private readonly Dictionary<string, (bool Existe, DateTime Quando)> _existe = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Fixado> Lista => _lista ??= Carregar();

    private IReadOnlyList<Fixado> Carregar()
    {
        if (repositorio.Ler() is { } lista) return ArrumarVersoes(lista);
        var importados = ImportadorFixados.Importar(ImportadorFixados.PastaDoWindows);
        Gravar(importados);
        return importados;
    }

    /// Com cache curto: a doca pergunta a cada mudança de janela, e um alvo na rede não pode travar a interface.
    public bool Existe(Fixado f)
    {
        var agora = DateTime.UtcNow;
        if (_existe.TryGetValue(f.Caminho, out var c) && agora - c.Quando < TimeSpan.FromSeconds(30)) return c.Existe;
        bool existe = f.Existe(File.Exists);
        _existe[f.Caminho] = (existe, agora);
        // App da loja atualizado: a pasta da versão antiga sumiu. A próxima leitura da lista passa para a atual.
        if (!existe && PacoteDaLoja.Ler(f.Caminho) is not null) _lista = null;
        return existe;
    }

    private IReadOnlyList<Fixado> ArrumarVersoes(IReadOnlyList<Fixado> lista)
    {
        var arrumada = PacoteDaLoja.Atualizar(lista, File.Exists, PastaDoPacote);
        if (!arrumada.SequenceEqual(lista)) Gravar(arrumada);
        return arrumada;
    }

    /// A pasta da versão instalada agora, pela família do pacote; nula se o app saiu.
    private static string? PastaDoPacote(string familia)
    {
        try
        {
            return new Windows.Management.Deployment.PackageManager().FindPackagesForUser(string.Empty, familia)
                .Select(p => p.InstalledLocation?.Path).FirstOrDefault(c => c is not null);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("FonteFixados.PastaDoPacote", e); return null; }
    }

    public object? Icone(Fixado f)
    {
        if (_icones.TryGetValue(f.Caminho, out var icone)) return icone;
        try { icone = IconImageConverter.GetImageFromAssociatedIcon(f.Caminho, IconSize.Large); }
        catch (Exception e) when (e is not OutOfMemoryException) { LogBarra.Erro("FonteFixados.Icone", e); icone = null; }
        return _icones[f.Caminho] = icone;
    }

    public void Salvar(IReadOnlyList<Fixado> lista)
    {
        _lista = lista;
        Gravar(lista);
    }

    private void Gravar(IReadOnlyList<Fixado> lista)
    {
        try { repositorio.Gravar(lista); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LogBarra.Erro("FonteFixados.Gravar", e); }
    }
}

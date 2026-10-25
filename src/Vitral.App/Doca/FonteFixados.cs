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
        if (repositorio.Ler() is { } lista) return lista;
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
        return existe;
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

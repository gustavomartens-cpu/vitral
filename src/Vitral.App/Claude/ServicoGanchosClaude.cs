using System.IO;
using Vitral.Core.Claude;

namespace Vitral.App.Claude;

/// Liga e desliga os avisos do Claude no settings.json do Claude Code (spec do notch vivo §4.2): cópia .vitral-bak antes
/// de gravar, troca por arquivo temporário e nunca grava um arquivo com defeito (o FormatException sobe).
internal sealed class ServicoGanchosClaude
{
    private readonly string _arquivo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    public bool EstaLigado()
    {
        try { return File.Exists(_arquivo) && GanchosClaude.EstaLigado(File.ReadAllText(_arquivo)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public void Ligar(string caminhoExe) => Gravar(t => GanchosClaude.Ligar(t, GanchosClaude.Comando(caminhoExe)));

    public void Desligar() => Gravar(GanchosClaude.Desligar);

    private void Gravar(Func<string, string> mudar)
    {
        string original = File.Exists(_arquivo) ? File.ReadAllText(_arquivo) : "";
        string novo = mudar(original); // com defeito: FormatException, nada é gravado
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        if (File.Exists(_arquivo)) File.Copy(_arquivo, _arquivo + ".vitral-bak", overwrite: true);
        string tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, novo);
        File.Move(tmp, _arquivo, overwrite: true);
    }
}

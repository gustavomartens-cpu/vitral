using System.IO;
using System.Security.Cryptography;

namespace Vitral.App.Agenda;

/// Os acessos às contas da agenda (spec da agenda §5.1): um arquivo por chave em %LOCALAPPDATA%\Vitral\contas\, cifrado
/// com a proteção de dados do Windows (só este usuário, nesta máquina, abre). Nunca vai para perfil, log ou config.
internal sealed class CofreTokens(string pasta)
{
    private static readonly byte[] Entropia = "Vitral.Agenda"u8.ToArray();

    public string Pasta { get; } = pasta;

    public void Gravar(string chave, byte[] dados)
    {
        Directory.CreateDirectory(Pasta);
        var cifrado = ProtectedData.Protect(dados, Entropia, DataProtectionScope.CurrentUser);
        string arquivo = Arquivo(chave), tmp = arquivo + ".tmp";
        File.WriteAllBytes(tmp, cifrado);
        File.Move(tmp, arquivo, overwrite: true);
    }

    public byte[]? Ler(string chave)
    {
        string arquivo = Arquivo(chave);
        if (!File.Exists(arquivo)) return null;
        try { return ProtectedData.Unprotect(File.ReadAllBytes(arquivo), Entropia, DataProtectionScope.CurrentUser); }
        catch (CryptographicException) { return null; } // de outro usuário ou corrompido: como se não houvesse
    }

    public void Apagar(string chave)
    {
        string arquivo = Arquivo(chave);
        if (File.Exists(arquivo)) File.Delete(arquivo);
    }

    public void ApagarComecandoCom(string prefixo)
    {
        if (!Directory.Exists(Pasta)) return;
        foreach (var f in Directory.GetFiles(Pasta, Seguro(prefixo) + "*.bin")) File.Delete(f);
    }

    private string Arquivo(string chave) => Path.Combine(Pasta, Seguro(chave) + ".bin");

    /// A chave vira nome de arquivo (e-mails têm @ e pontos, que podem ficar; o resto vira _).
    private static string Seguro(string chave) =>
        new(chave.Select(c => char.IsLetterOrDigit(c) || c is '@' or '.' or '-' or '_' ? c : '_').ToArray());
}

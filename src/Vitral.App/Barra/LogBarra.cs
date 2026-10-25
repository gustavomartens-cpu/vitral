using System.IO;
using Vitral.Core.Windows;

namespace Vitral.App.Barra;

/// Log da barra do topo (%LOCALAPPDATA%\Vitral\logs\barra.log). Nunca lança: a barra não pode derrubar o app por causa do log.
internal static class LogBarra
{
    private static readonly object Trava = new();

    public static void Info(string mensagem) => Escrever($"{DateTimeOffset.Now:O} [info] {mensagem}");

    public static void Erro(string origem, Exception e) => Escrever($"{DateTimeOffset.Now:O} [{origem}] {e}");

    private static void Escrever(string linha)
    {
        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Pastas.Logs);
                File.AppendAllText(Path.Combine(Pastas.Logs, "barra.log"), linha + Environment.NewLine);
            }
        }
        catch { /* sem log, segue */ }
    }
}

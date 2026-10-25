using System.IO;
using Vitral.Core.Windows;

namespace Vitral.App.Barra;

/// Log da barra do topo (%LOCALAPPDATA%\Vitral\logs\barra.log). Nunca lança: a barra não pode derrubar o app por causa do log.
internal static class LogBarra
{
    private static readonly object Trava = new();

    public static void Erro(string origem, Exception e)
    {
        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Pastas.Logs);
                File.AppendAllText(Path.Combine(Pastas.Logs, "barra.log"), $"{DateTimeOffset.Now:O} [{origem}] {e}{Environment.NewLine}");
            }
        }
        catch { /* sem log, segue */ }
    }
}

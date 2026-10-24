namespace Vitral.Core.Windows;

/// Pastas do app em %LOCALAPPDATA%\Vitral (spec §4.1).
public static class Pastas
{
    public static string Raiz { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vitral");

    public static string Perfis => Path.Combine(Raiz, "perfis");
    public static string Retratos => Path.Combine(Raiz, "retratos");
    public static string Logs => Path.Combine(Raiz, "logs");
}

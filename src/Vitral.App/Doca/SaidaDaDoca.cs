using System.Diagnostics;
using System.IO;
using Vitral.Core.Barra;
using Vitral.Core.Configuracao;
using Vitral.Core.Perfis;
using Vitral.Core.Windows;

namespace Vitral.App.Doca;

/// Saída sem o Sair() de sempre: a desinstalação (gancho do Velopack, num processo próprio). Fecha o Vitral que
/// estiver rodando, mostra a barra do Windows, devolve o "ocultar automaticamente" guardado e os ícones da bandeja.
internal static class SaidaDaDoca
{
    public static void AoDesinstalar()
    {
        try
        {
            string? eu = Environment.ProcessPath;
            foreach (var p in Process.GetProcessesByName("Vitral"))
            {
                try
                {
                    if (p.Id == Environment.ProcessId || !string.Equals(p.MainModule?.FileName, eu, StringComparison.OrdinalIgnoreCase)) continue;
                    p.Kill();
                    p.WaitForExit(3000);
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { /* já saiu */ }
            }
            var config = new ServicoConfig(Path.Combine(Pastas.Raiz, "config.json"));
            var barra = new BarraDoWindows(new RegistroWindows(), new NotificadorWindows());
            new EstadoDoca(new SemDoca(), barra, config).Encerrar();
            FonteBandeja.DevolverIcones();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("SaidaDaDoca.AoDesinstalar", e); }
    }

    private sealed class SemDoca : IControleDoca
    {
        public void Aplicar(BarraBaixoPerfil estado) { }
    }
}

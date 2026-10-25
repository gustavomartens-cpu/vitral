using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Vitral.App.Barra;

/// Ações dos cliques nas ilhas (spec §4): os atalhos do próprio Windows, para abrir os painéis nativos.
internal static partial class Atalhos
{
    private const byte VkWin = 0x5B;
    private const uint SoltarTecla = 0x0002;

    [LibraryImport("user32.dll")] private static partial void keybd_event(byte tecla, byte varredura, uint flags, UIntPtr extra);

    /// Win+N: central de notificações com o calendário.
    public static void CentralDeNotificacoes() => WinMais((byte)'N');

    /// Win+A: configurações rápidas (Wi-Fi, volume, bateria).
    public static void ConfiguracoesRapidas() => WinMais((byte)'A');

    public static void GerenciadorDeTarefas()
    {
        try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
        catch (Exception e) { LogBarra.Erro("Atalhos.Gerenciador", e); }
    }

    private static void WinMais(byte tecla)
    {
        try
        {
            keybd_event(VkWin, 0, 0, UIntPtr.Zero);
            keybd_event(tecla, 0, 0, UIntPtr.Zero);
            keybd_event(tecla, 0, SoltarTecla, UIntPtr.Zero);
            keybd_event(VkWin, 0, SoltarTecla, UIntPtr.Zero);
        }
        catch (Exception e) { LogBarra.Erro("Atalhos.Teclas", e); }
    }
}

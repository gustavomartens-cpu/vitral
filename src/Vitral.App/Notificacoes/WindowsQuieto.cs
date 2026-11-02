using Microsoft.Win32;
using Vitral.Core.Configuracao;
using Vitral.Core.Notificacoes;

namespace Vitral.App.Notificacoes;

/// O balão do Windows calado enquanto o Vitral mostra as notificações no notch (spec das notificações §4, caminho
/// escolhido na prova): o DWORD ShowBanner = 0 na configuração de notificações de cada app vale na hora, e a notificação
/// continua indo para a central (e para o Vitral). O que foi calado fica no config, para devolver na saída ou, depois de
/// uma queda, na abertura seguinte.
internal sealed class WindowsQuieto(ServicoConfig config)
{
    private const string Raiz = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
    private readonly object _trava = new();

    /// Cala todos os apps que já têm configuração de notificação e os <paramref name="novos"/> (o Claude e quem já estava
    /// calado ficam como estão).
    public void Calar(IEnumerable<string> novos)
    {
        lock (_trava)
        {
            try
            {
                using var raiz = Registry.CurrentUser.CreateSubKey(Raiz);
                var apps = raiz.GetSubKeyNames().Concat(novos).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(a => { using var k = raiz.OpenSubKey(a); return (a, k?.GetValue("ShowBanner") as int?); }).ToList();
                var calar = RegrasNotificacoes.ParaCalar(apps);
                if (calar.Count == 0) return;
                var gravados = RegrasNotificacoes.ParaDevolver(config.Ler().BannersDesligados).ToHashSet(StringComparer.OrdinalIgnoreCase);
                gravados.UnionWith(calar);
                config.Gravar(config.Ler() with { BannersDesligados = gravados.ToList() }); // antes de calar: uma queda no meio ainda devolve
                foreach (var app in calar)
                {
                    using var k = raiz.CreateSubKey(app);
                    k.SetValue("ShowBanner", 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("WindowsQuieto.Calar", e); }
        }
    }

    /// Devolve o balão de tudo o que o Vitral calou (apaga o ShowBanner: volta ao padrão do Windows, mostrar).
    public void Devolver()
    {
        lock (_trava)
        {
            try
            {
                var gravados = RegrasNotificacoes.ParaDevolver(config.Ler().BannersDesligados);
                if (gravados.Count == 0) return;
                using var raiz = Registry.CurrentUser.CreateSubKey(Raiz);
                foreach (var app in gravados)
                {
                    using var k = raiz.OpenSubKey(app, writable: true);
                    k?.DeleteValue("ShowBanner", throwOnMissingValue: false);
                }
                config.Gravar(config.Ler() with { BannersDesligados = null });
            }
            catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("WindowsQuieto.Devolver", e); }
        }
    }
}

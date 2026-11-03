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

    /// Marca, ao lado do ShowBanner, os apps que o Vitral calou: uma versão antiga do Vitral que regrave o config sem a
    /// lista não faz o balão ficar calado para sempre.
    private const string Marca = "VitralCalou";
    private bool _encerrado;

    /// Na saída: devolve e não cala mais (nada que chegue depois pode deixar o Windows calado).
    public void Encerrar()
    {
        lock (_trava) _encerrado = true;
        Devolver();
    }

    /// Na desinstalação, sem o Vitral aberto: devolve pelo config.
    public static void DevolverDoConfig() =>
        new WindowsQuieto(new ServicoConfig(System.IO.Path.Combine(Core.Windows.Pastas.Raiz, "config.json"))).Encerrar();

    /// Cala todos os apps que já têm configuração de notificação e os <paramref name="novos"/> (o Claude e quem já estava
    /// calado ficam como estão).
    public void Calar(IEnumerable<string> novos)
    {
        lock (_trava)
        {
            if (_encerrado) return;
            try
            {
                using var raiz = Registry.CurrentUser.CreateSubKey(Raiz);
                var apps = raiz.GetSubKeyNames().Concat(novos).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(a => { using var k = raiz.OpenSubKey(a); return (a, k?.GetValue("ShowBanner") as int?); }).ToList();
                var calar = RegrasNotificacoes.ParaCalar(apps);
                if (calar.Count == 0) return;
                var gravados = RegrasNotificacoes.ParaDevolver(config.Ler().BannersDesligados).ToHashSet(StringComparer.OrdinalIgnoreCase);
                gravados.UnionWith(calar);
                config.Atualizar(c => c with { BannersDesligados = gravados.ToList() }); // antes de calar: uma queda no meio ainda devolve
                foreach (var app in calar)
                {
                    using var k = raiz.CreateSubKey(app);
                    k.SetValue(Marca, 1, RegistryValueKind.DWord); // antes do ShowBanner, pelo mesmo motivo
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
                using var raiz = Registry.CurrentUser.CreateSubKey(Raiz);
                var (devolver, soMarca) = RegrasNotificacoes.ParaDevolver(config.Ler().BannersDesligados, Marcados(raiz));
                foreach (var app in devolver) Apagar(raiz, app, "ShowBanner", Marca);
                foreach (var app in soMarca) Apagar(raiz, app, Marca);
                if (devolver.Count > 0) config.Atualizar(c => c with { BannersDesligados = null });
            }
            catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("WindowsQuieto.Devolver", e); }
        }
    }

    /// Os apps com a marca e o ShowBanner atual. Uma chave ilegível (de qualquer app) fica de fora, sem impedir a
    /// devolução dos outros.
    private static List<(string, int?)> Marcados(RegistryKey raiz)
    {
        var lista = new List<(string, int?)>();
        foreach (var app in raiz.GetSubKeyNames())
        {
            try
            {
                using var k = raiz.OpenSubKey(app);
                if (k?.GetValue(Marca) is not null) lista.Add((app, k.GetValue("ShowBanner") as int?));
            }
            catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro($"WindowsQuieto.Marcados {app}", e); }
        }
        return lista;
    }

    /// Apaga os valores de um app; a falha num app não impede os outros.
    private static void Apagar(RegistryKey raiz, string app, params string[] valores)
    {
        try
        {
            using var k = raiz.OpenSubKey(app, writable: true);
            foreach (var v in valores) k?.DeleteValue(v, throwOnMissingValue: false);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro($"WindowsQuieto.Apagar {app}", e); }
    }
}

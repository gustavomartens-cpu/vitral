using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Vitral.Core.Notificacoes;
using Vitral.Core.Windows;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Vitral.App.Notificacoes;

/// A identidade de pacote do Vitral (spec das notificações §2): um pacote esparso, registrado pelo próprio Vitral, que
/// aponta para a pasta do Vitral.exe. O app.manifest liga o exe ao pacote; sem ele registrado, o Windows ignora a ligação
/// e o Vitral roda sem identidade. O registro só vale para os processos abertos depois dele: o Vitral se reabre uma vez.
internal static class PacoteVitral
{
    public const string ArgumentoReaberto = "--reaberto";
    // O Vitral de desenvolvimento tem um pacote só dele: sem isso, ele e o instalado roubariam a identidade um do outro a
    // cada abertura (revisão final da 0.15.0). O app.Debug.manifest aponta para o Vitral.Dev.
#if DEBUG
    private const string Nome = "Vitral.Dev", Sufixo = "-dev";
#else
    private const string Nome = "Vitral", Sufixo = "";
#endif
    private const string Editor = "CN=Vitral";

    private static readonly string Pasta = Path.Combine(Pastas.Raiz, "pacote" + Sufixo);
    private static readonly string Registrado = Path.Combine(Pasta, "registrado.txt");

    public static bool TemIdentidade
    {
        get
        {
            try { _ = Package.Current.Id; return true; }
            catch (InvalidOperationException) { return false; }
        }
    }

    private static bool ModoDesenvolvedor =>
        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock")?.GetValue("AllowDevelopmentWithoutDevLicense") is 1;

    /// Ao abrir, antes da instância única: sem identidade, com o modo de desenvolvedor e sem o pacote registrado para esta
    /// pasta, registra e abre outro Vitral (que já nasce com identidade). True = reabriu, e este deve sair sem fazer nada.
    public static bool RegistrarEReabrir(string[] args)
    {
        try
        {
            if (TemIdentidade || !ModoDesenvolvedor || args.Contains(ArgumentoReaberto)) return false;
            string pastaDoExe = AppContext.BaseDirectory.TrimEnd('\\');
            string? registrada = File.Exists(Registrado) ? File.ReadAllText(Registrado).Trim() : null;
            if (!RegrasNotificacoes.PrecisaRegistrar(registrada, pastaDoExe) && Existe()) return false;

            Directory.CreateDirectory(Path.Combine(Pasta, "Assets"));
            File.WriteAllText(Path.Combine(Pasta, "AppxManifest.xml"), Manifesto());
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Recursos", "PacoteLogo.png"), Path.Combine(Pasta, "Assets", "Logo.png"), overwrite: true);
            var opcoes = new RegisterPackageOptions { ExternalLocationUri = new Uri(pastaDoExe + "\\"), DeveloperMode = true, ForceAppShutdown = false };
            var resultado = new PackageManager().RegisterPackageByUriAsync(new Uri(Path.Combine(Pasta, "AppxManifest.xml")), opcoes).AsTask();
            if (!resultado.Wait(TimeSpan.FromSeconds(30)))
            {
                Barra.LogBarra.Info("Pacote do Vitral não registrado: tempo esgotado");
                return false;
            }
            if (resultado.Result.ExtendedErrorCode is not null)
            {
                Barra.LogBarra.Info($"Pacote do Vitral não registrado: {resultado.Result.ErrorText}");
                return false;
            }
            File.WriteAllText(Registrado, pastaDoExe);
            Barra.LogBarra.Info($"Pacote do Vitral registrado para {pastaDoExe}; reabrindo com identidade");
            var novo = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            foreach (var a in args) novo.ArgumentList.Add(a);
            novo.ArgumentList.Add(ArgumentoReaberto);
            Process.Start(novo);
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Barra.LogBarra.Erro("PacoteVitral", e);
            return false;
        }
    }

    private static bool Existe() => new PackageManager().FindPackagesForUser("", Nome, Editor).Any();

    /// Na desinstalação: o balão do Windows volta e o pacote sai (ele apontaria para uma pasta apagada).
    public static void AoDesinstalar()
    {
        try { WindowsQuieto.DevolverDoConfig(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("PacoteVitral.Desinstalar", e); }
        try
        {
            var gerente = new PackageManager();
            foreach (var p in gerente.FindPackagesForUser("", Nome, Editor).ToList())
                gerente.RemovePackageAsync(p.Id.FullName).AsTask().Wait(TimeSpan.FromSeconds(30));
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("PacoteVitral.Desinstalar", e); }
    }

    private static string Manifesto() => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                 xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
                 xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
                 xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
                 IgnorableNamespaces="uap uap3 uap10 rescap">
          <Identity Name="{Nome}" Publisher="{Editor}" Version="1.0.0.0" ProcessorArchitecture="neutral" />
          <Properties>
            <DisplayName>Vitral</DisplayName>
            <PublisherDisplayName>Vitral</PublisherDisplayName>
            <Logo>Assets\Logo.png</Logo>
            <uap10:AllowExternalContent>true</uap10:AllowExternalContent>
          </Properties>
          <Dependencies>
            <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26200.0" />
          </Dependencies>
          <Resources><Resource Language="pt-br" /></Resources>
          <Capabilities>
            <rescap:Capability Name="runFullTrust" />
            <rescap:Capability Name="unvirtualizedResources" />
            <uap3:Capability Name="userNotificationListener" />
          </Capabilities>
          <Applications>
            <Application Id="Vitral" Executable="Vitral.exe" uap10:TrustLevel="mediumIL" uap10:RuntimeBehavior="win32App">
              <uap:VisualElements DisplayName="Vitral" Description="Vitral" BackgroundColor="transparent"
                                  Square150x150Logo="Assets\Logo.png" Square44x44Logo="Assets\Logo.png" AppListEntry="none" />
            </Application>
          </Applications>
        </Package>
        """;
}

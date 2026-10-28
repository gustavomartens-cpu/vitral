using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

public partial class PainelCalendario : UserControl
{
    public PainelCalendario() => InitializeComponent();

    /// O compromisso abre no Google Agenda ou no Outlook, no navegador.
    private void Compromisso_Clique(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ItemCompromisso { LinkEvento: { } link }) Abrir(link);
    }

    /// "Entrar": o link da reunião (Meet, Teams, Zoom).
    private void Entrar_Clique(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // não abre também o evento
        if (((FrameworkElement)sender).DataContext is ItemCompromisso { LinkReuniao: { } link }) Abrir(link);
    }

    internal static void Abrir(Uri link)
    {
        // O link vem do convite, de terceiros: o Windows só abre endereço da web (nada de file:, UNC, ms-msdt:...).
        if (link.Scheme != Uri.UriSchemeHttps && link.Scheme != Uri.UriSchemeHttp) return;
        try { Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { LogBarra.Erro("Abrir link", e); }
    }
}

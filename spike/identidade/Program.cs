// Prova da identidade de pacote (DESCARTÁVEL).
//   (sem argumento) pede acesso e lista as notificações da central
//   evento          assina NotificationChanged, manda uma notificação de teste e conta os avisos em 6 s
//   mandar <texto>  só manda uma notificação de teste
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

try { Console.WriteLine($"Identidade: {Package.Current.Id.FamilyName}"); }
catch (InvalidOperationException) { Console.WriteLine("SEM identidade de pacote"); return 1; }

void Mandar(string texto)
{
    var xml = new XmlDocument();
    xml.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>Vitral (prova)</text><text>{texto}</text></binding></visual></toast>");
    ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(xml));
    Console.WriteLine($"Mandada: {texto}");
}

if (args is ["mandar", var t]) { Mandar(t); return 0; }

var ouvinte = UserNotificationListener.Current;
var acesso = await ouvinte.RequestAccessAsync();
Console.WriteLine($"Acesso: {acesso}");
if (acesso != UserNotificationListenerAccessStatus.Allowed) return 2;

if (args is ["evento"])
{
    int avisos = 0;
    try
    {
        ouvinte.NotificationChanged += (_, e) => { avisos++; Console.WriteLine($"EVENTO {e.ChangeKind} id={e.UserNotificationId}"); };
        Console.WriteLine("Evento assinado");
    }
    catch (Exception e) { Console.WriteLine($"Evento recusado: {e.GetType().Name} 0x{e.HResult:X8} {e.Message}"); }
    Mandar("teste do evento");
    await Task.Delay(6000);
    Console.WriteLine($"Avisos recebidos: {avisos}");
    var depois = await ouvinte.GetNotificationsAsync(NotificationKinds.Toast);
    Console.WriteLine($"Da prova na central: {depois.Count(n => n.AppInfo?.PackageFamilyName == Package.Current.Id.FamilyName)}");
    return 0;
}

var lista = await ouvinte.GetNotificationsAsync(NotificationKinds.Toast);
Console.WriteLine($"Notificações: {lista.Count}");
foreach (var n in lista)
{
    var textos = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements().Select(x => x.Text) ?? [];
    Console.WriteLine($"[{n.CreationTime.LocalDateTime:dd/MM HH:mm}] {n.AppInfo?.DisplayInfo.DisplayName} ({n.AppInfo?.AppUserModelId}): {string.Join(" | ", textos)}");
}
return 0;

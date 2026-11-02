// Prova da identidade de pacote (DESCARTÁVEL): com o pacote esparso registrado, o exe pede acesso às notificações e
// lista as que estão na central, com o nome do app.
using Windows.ApplicationModel;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

try { Console.WriteLine($"Identidade: {Package.Current.Id.FamilyName}"); }
catch (InvalidOperationException) { Console.WriteLine("SEM identidade de pacote"); return 1; }

var ouvinte = UserNotificationListener.Current;
var acesso = await ouvinte.RequestAccessAsync();
Console.WriteLine($"Acesso: {acesso}");
if (acesso != UserNotificationListenerAccessStatus.Allowed) return 2;

var lista = await ouvinte.GetNotificationsAsync(NotificationKinds.Toast);
Console.WriteLine($"Notificações: {lista.Count}");
foreach (var n in lista.Take(10))
{
    var textos = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements().Select(t => t.Text) ?? [];
    Console.WriteLine($"[{n.CreationTime.LocalDateTime:dd/MM HH:mm}] {n.AppInfo?.DisplayInfo.DisplayName}: {string.Join(" | ", textos)}");
}
return 0;

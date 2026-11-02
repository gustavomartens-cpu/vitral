// Prova das notificações pelo banco do Windows (DESCARTÁVEL): copia wpndatabase.db (+ wal) e lista o esquema e as
// notificações recentes. Uso: LerNotificacoes [esquema]
using Microsoft.Data.Sqlite;

string origem = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Notifications");
string copia = Path.Combine(Path.GetTempPath(), "vitral-wpn-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(copia);
foreach (var nome in new[] { "wpndatabase.db", "wpndatabase.db-wal", "wpndatabase.db-shm" })
{
    string de = Path.Combine(origem, nome);
    if (!File.Exists(de)) continue;
    using var entrada = new FileStream(de, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    using var saida = File.Create(Path.Combine(copia, nome));
    entrada.CopyTo(saida);
}
using var c = new SqliteConnection($"Data Source={Path.Combine(copia, "wpndatabase.db")}");
c.Open();

if (args.Contains("esquema"))
{
    using var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='table'";
    using var r = cmd.ExecuteReader();
    while (r.Read()) Console.WriteLine($"{r.GetString(0)}: {r.GetString(1)}\n");
    return;
}

if (args.Contains("apps"))
{
    using var cmd = c.CreateCommand();
    cmd.CommandText = """
        SELECT h.PrimaryId, h.HandlerType, (SELECT COUNT(*) FROM Notification n WHERE n.HandlerId = h.RecordId),
               (SELECT group_concat(a.AssetKey || '=' || substr(a.AssetValue, 1, 90), ' ; ') FROM HandlerAssets a WHERE a.HandlerId = h.RecordId)
        FROM NotificationHandler h ORDER BY 3 DESC LIMIT 15
        """;
    using var r = cmd.ExecuteReader();
    while (r.Read()) Console.WriteLine($"{r.GetInt64(2),3}  {r.GetString(0)} [{(r.IsDBNull(1) ? "" : r.GetString(1))}]  {(r.IsDBNull(3) ? "" : r.GetString(3))}");
    return;
}

using (var cmd = c.CreateCommand())
{
    cmd.CommandText = """
        SELECT h.PrimaryId, n.Type, n.ArrivalTime, n.ExpiryTime, n.Payload
        FROM Notification n JOIN NotificationHandler h ON h.RecordId = n.HandlerId
        ORDER BY n.ArrivalTime DESC LIMIT 12
        """;
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        string app = r.GetString(0);
        string tipo = r.IsDBNull(1) ? "" : r.GetString(1);
        var chegou = DateTime.FromFileTimeUtc(r.GetInt64(2)).ToLocalTime();
        string payload = r.IsDBNull(4) ? "" : System.Text.Encoding.UTF8.GetString((byte[])r[4]);
        Console.WriteLine($"[{chegou:dd/MM HH:mm}] {app} ({tipo})");
        Console.WriteLine("   " + (payload.Length > 400 ? payload[..400] + "…" : payload).Replace("\n", " "));
    }
}

using System.Data.SQLite;
var conn = new SQLiteConnection("Data Source=JumongPos.db");
conn.Open();
var cmd = new SQLiteCommand("SELECT Key, Value FROM Settings WHERE Key IN ('StoreId','StoreName','CloudApiUrl','LastMasterSync')", conn);
using var rdr = cmd.ExecuteReader();
while (rdr.Read()) Console.WriteLine($"{rdr["Key"]} = {rdr["Value"]}");

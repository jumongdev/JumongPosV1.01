using System;
using System.Data.SQLite;

var conn = new SQLiteConnection("Data Source=C:\\Users\\ADMIN\\Desktop\\JumongPosV1.01\\JumongPos.db");
conn.Open();
var cmd = new SQLiteCommand("SELECT Key, Value FROM Settings WHERE Key LIKE '%tore%' OR Key = 'CloudApiUrl'", conn);
var r = cmd.ExecuteReader();
while (r.Read()) Console.WriteLine($"{r["Key"]} = {r["Value"]}");
conn.Close();

using System;
using System.Data.SQLite;
var conn = new SQLiteConnection("Data Source=C:\\JumongAPI\\client\\JumongPos.db");
conn.Open();
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Id, Username, Password, Role, FullName FROM Users";
var r = cmd.ExecuteReader();
while (r.Read()) Console.WriteLine($"ID={r[0]}, User={r[1]}, Pass={r[2]}, Role={r[3]}, Name={r[4]}");
conn.Close();

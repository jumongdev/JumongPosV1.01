using System;
using System.Data.SQLite;

var conn = new SQLiteConnection("Data Source=C:\\Users\\ADMIN\\Desktop\\JumongPosV1.01\\JumongPos.db");
conn.Open();
var cmd = new SQLiteCommand("SELECT Id, Name, Barcode, Price, Cost FROM Products WHERE Cost = 0 OR Cost IS NULL ORDER BY Name", conn);
var r = cmd.ExecuteReader();
int count = 0;
Console.WriteLine("ID\tName\tBarcode\tPrice\tCost");
Console.WriteLine(new string('-', 80));
while (r.Read())
{
    count++;
    Console.WriteLine($"{r["Id"]}\t{r["Name"]}\t{r["Barcode"]}\t{r["Price"]}\t{r["Cost"]}");
}
Console.WriteLine(new string('-', 80));
Console.WriteLine($"Total products with no cost: {count}");

using System;
using System.Data.SQLite;
using System.Globalization;

var dbPath = "JumongPos.db";
if (args.Length > 0) dbPath = args[0];

using var conn = new SQLiteConnection($"Data Source={dbPath}");
conn.Open();

// 1. Get the latest end shift
using var dcCmd = new SQLiteCommand(@"
    SELECT CloseDate, TotalInventoryCost, TotalCostSold, TotalStockReceivedCost 
    FROM DailyClose ORDER BY Id DESC LIMIT 1", conn);
using var dcRdr = dcCmd.ExecuteReader();
if (!dcRdr.Read()) { Console.WriteLine("No end shift found."); return; }

var closeDate = dcRdr.GetString(0);
var totalInvCost = dcRdr.GetDecimal(1);
var totalCOGS = dcRdr.GetDecimal(2);
var totalRecvCost = dcRdr.GetDecimal(3);

// Find previous shift's inventory cost
using var prevCmd = new SQLiteCommand(@"
    SELECT TotalInventoryCost FROM DailyClose 
    WHERE CloseDate < @cd ORDER BY Id DESC LIMIT 1", conn);
prevCmd.Parameters.AddWithValue("@cd", closeDate);
var prevObj = prevCmd.ExecuteScalar();
var prevInvCost = prevObj != DBNull.Value && prevObj != null ? Convert.ToDecimal(prevObj) : 0m;

var expected = prevInvCost + totalRecvCost - totalCOGS;
var variance = totalInvCost - expected;

Console.WriteLine("=== INVENTORY RECONCILIATION ===");
Console.WriteLine($"Close Date:     {closeDate}");
Console.WriteLine($"Previous Inv:   {prevInvCost,14:N2}");
Console.WriteLine($"+ Received:     {totalRecvCost,14:N2}");
Console.WriteLine($"- COGS:         {totalCOGS,14:N2}");
Console.WriteLine($"= Expected:     {expected,14:N2}");
Console.WriteLine($"Actual Inv:     {totalInvCost,14:N2}");
Console.WriteLine($"Variance:       {variance,14:N2} {(variance == 0 ? "BALANCED" : (variance > 0 ? "OVER" : "SHORT"))}");
Console.WriteLine();

if (variance == 0) { Console.WriteLine("No variance."); return; }

// 2. Sales where unit cost != product current cost
Console.WriteLine("=== SALE ITEMS WITH COST MISMATCH ===");
using var saleCmd = new SQLiteCommand(@"
    SELECT si.ProductName, si.Quantity, si.UnitCost, 
           COALESCE(p.Cost, 0) AS ProdCost, si.UnitCost / CAST(si.Quantity AS REAL) AS PerUnitSale, 
           (COALESCE(p.Cost, 0) - si.UnitCost / CAST(si.Quantity AS REAL)) * si.Quantity AS Impact
    FROM SaleItems si
    JOIN Sales s ON si.SaleId = s.Id
    LEFT JOIN Products p ON si.ProductId = p.Id
    WHERE si.IsVoided = 0 AND s.IsVoided = 0 AND s.SaleDate >= @since
      AND si.Quantity > 0
      AND ABS(COALESCE(p.Cost, 0) - si.UnitCost / CAST(si.Quantity AS REAL)) > 0.005
    ORDER BY ABS(Impact) DESC
    LIMIT 30", conn);
saleCmd.Parameters.AddWithValue("@since", closeDate);
using var sRdr = saleCmd.ExecuteReader();
var hasSale = false;
var saleImpactTotal = 0m;
Console.WriteLine($"{"Product",-40} {"Qty",5} {"UnitCost",10} {"PerUnitSale",11} {"ProdCost",10} {"Impact",12}");
Console.WriteLine(new string('-', 93));
while (sRdr.Read())
{
    hasSale = true;
    var name = sRdr.GetString(0).Length > 38 ? sRdr.GetString(0)[..38] : sRdr.GetString(0);
    var qty = sRdr.GetInt32(1);
    var unitCost = sRdr.GetDecimal(2);
    var prodCost = sRdr.GetDecimal(3);
    var perUnit = sRdr.GetDouble(4);
    var impact = sRdr.GetDouble(5);
    saleImpactTotal += (decimal)impact;
    Console.WriteLine($"{name,-40} {qty,5} {unitCost,10:N2} {perUnit,11:N4} {prodCost,10:N2} {impact,12:N2}");
}
if (!hasSale) Console.WriteLine("  (all sale items match product costs)");
Console.WriteLine($"  Sale mismatch subtotal: {saleImpactTotal,12:N2}");
Console.WriteLine();

// 3. Products where cost changed (most impactful)
Console.WriteLine("=== PRODUCTS — COST COMPARISON WITH PREV. SHIFT ===");
// We can approximate: if a product was in previous dailyclose inventory 
// and had sales + receiving today, compare current cost with sale-time cost
using var chgCmd = new SQLiteCommand(@"
    SELECT Name, StockQty, Cost FROM Products 
    WHERE IsActive = 1 AND StockQty > 0 AND Cost > 0
    ORDER BY StockQty * Cost DESC", conn);
using var chgRdr = chgCmd.ExecuteReader();
Console.WriteLine($"{"Product",-40} {"StockQty",8} {"Cost",10} {"Value",12}");
Console.WriteLine(new string('-', 75));
while (chgRdr.Read())
{
    var name = chgRdr.GetString(0).Length > 38 ? chgRdr.GetString(0)[..38] : chgRdr.GetString(0);
    var qty = chgRdr.GetInt32(1);
    var cost = chgRdr.GetDecimal(2);
    var val = qty * cost;
    Console.WriteLine($"{name,-40} {qty,8:N0} {cost,10:N2} {val,12:N2}");
}
Console.WriteLine();

// 4. Receiving — recompute
Console.WriteLine("=== RECEIVING TODAY (Cost x QtyAdded) ===");
using var recvCmd = new SQLiteCommand(@"
    SELECT st.ProductName, SUM(st.QuantityAdded), COALESCE(p.Cost, 0)
    FROM StockTrail st
    LEFT JOIN Products p ON st.ProductId = p.Id
    WHERE st.QuantityAdded > 0 AND st.CreatedAt >= @since
    GROUP BY st.ProductId, st.ProductName
    ORDER BY st.ProductName", conn);
recvCmd.Parameters.AddWithValue("@since", closeDate);
using var rRdr = recvCmd.ExecuteReader();
var calcRecvCost = 0m;
Console.WriteLine($"{"Product",-40} {"Qty",7} {"Cost",10} {"ExtValue",12}");
Console.WriteLine(new string('-', 75));
while (rRdr.Read())
{
    var name = rRdr.GetString(0).Length > 38 ? rRdr.GetString(0)[..38] : rRdr.GetString(0);
    var qty = rRdr.GetDecimal(1);
    var cost = rRdr.GetDecimal(2);
    var ext = qty * cost;
    calcRecvCost += ext;
    Console.WriteLine($"{name,-40} {qty,7:N0} {cost,10:N2} {ext,12:N2}");
}
Console.WriteLine($"  Calc RecvCost:  {calcRecvCost,12:N2}");
Console.WriteLine($"  Stored RecvCost:{totalRecvCost,12:N2}");
Console.WriteLine($"  Diff:           {calcRecvCost - totalRecvCost,12:N2}");
Console.WriteLine();

// 5. Quick check: products that might have cost-zero discrepancy
Console.WriteLine("=== PRODUCTS WITH ZERO OR NULL COST ===");
using var zcCmd = new SQLiteCommand("SELECT Name, StockQty, Cost FROM Products WHERE IsActive = 1 AND StockQty > 0 AND (Cost = 0 OR Cost IS NULL)", conn);
using var zcRdr = zcCmd.ExecuteReader();
var foundZc = false;
while (zcRdr.Read()) { foundZc = true; Console.WriteLine($"  {zcRdr.GetString(0)} (stock: {zcRdr.GetInt32(1)})"); }
if (!foundZc) Console.WriteLine("  (none — all products have valid costs)");
Console.WriteLine();

Console.WriteLine("=== SUMMARY ===");
Console.WriteLine($"Variance:      {variance,12:N2}");
Console.WriteLine($"Sale mismatch: {saleImpactTotal,12:N2}");
Console.WriteLine($"RecvCost diff: {calcRecvCost - totalRecvCost,12:N2}");
Console.WriteLine($"Unexplained:   {variance - saleImpactTotal - (calcRecvCost - totalRecvCost),12:N2}");

using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class CreditService
{
    public static void AddTransaction(int customerId, int? saleId, string type, string description, decimal amount, string paymentMethod = "", string referenceNo = "", int userId = 0, string userName = "")
    {
        var customer = CustomerService.GetById(customerId);
        if (customer == null) return;

        var newBalance = type is "Payment" or "Void" or "Adjustment" ? customer.CreditBalance - amount : customer.CreditBalance + amount;

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        var sql = @"INSERT INTO CreditTransactions (CustomerId, SaleId, Type, Description, Debit, Credit, Balance, PaymentMethod, ReferenceNo, UserId, UserName)
                      VALUES (@cid, @sid, @t, @d, @db, @cr, @bal, @pm, @ref, @uid, @uname)";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@sid", (object?)saleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@t", type);
        cmd.Parameters.AddWithValue("@d", description);
        cmd.Parameters.AddWithValue("@db", type == "Sale" ? amount : 0);
        cmd.Parameters.AddWithValue("@cr", type is "Payment" or "Void" or "Adjustment" ? amount : 0);
        cmd.Parameters.AddWithValue("@bal", newBalance);
        cmd.Parameters.AddWithValue("@pm", paymentMethod);
        cmd.Parameters.AddWithValue("@ref", referenceNo);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@uname", userName);
        cmd.ExecuteNonQuery();

        var upd = new SQLiteCommand("UPDATE Customers SET CreditBalance = @bal WHERE Id = @id", conn);
        upd.Parameters.AddWithValue("@bal", newBalance);
        upd.Parameters.AddWithValue("@id", customerId);
        upd.ExecuteNonQuery();

        trans.Commit();
        _ = SyncService.SyncCreditTransaction(new CreditTransaction { CustomerId = customerId, SaleId = saleId, Type = type, Description = description, Debit = amount > 0 ? amount : 0, Credit = amount < 0 ? Math.Abs(amount) : 0, Balance = newBalance, PaymentMethod = paymentMethod, ReferenceNo = referenceNo, UserId = userId, UserName = userName, CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    public static List<CreditTransaction> GetAll()
    {
        var list = new List<CreditTransaction>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT ct.*, c.Name AS CustomerName, s.InvoiceNo
                     FROM CreditTransactions ct
                     LEFT JOIN Customers c ON c.Id = ct.CustomerId
                     LEFT JOIN Sales s ON s.Id = ct.SaleId
                     ORDER BY ct.Id";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(MapTransaction(rdr));
        return list;
    }

    public static List<CreditTransaction> GetByCustomer(int customerId, DateTime? from = null, DateTime? to = null)
    {
        var list = new List<CreditTransaction>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT ct.*, c.Name as CustomerName, s.InvoiceNo
                     FROM CreditTransactions ct
                     LEFT JOIN Customers c ON c.Id = ct.CustomerId
                     LEFT JOIN Sales s ON s.Id = ct.SaleId
                     WHERE ct.CustomerId = @cid";
        if (from.HasValue) sql += " AND date(ct.CreatedAt) >= date(@from)";
        if (to.HasValue) sql += " AND date(ct.CreatedAt) <= date(@to)";
        sql += " ORDER BY ct.Id DESC";

        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", customerId);
        if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd"));
        if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd"));
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapTransaction(rdr));
        }
        return list;
    }

    public static List<CreditTransaction> GetAllPayments(DateTime? from = null, DateTime? to = null, string? paymentMethod = null, string? customerKeyword = null)
    {
        var list = new List<CreditTransaction>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT ct.*, c.Name as CustomerName, c.Phone as CustomerPhone, s.InvoiceNo
                     FROM CreditTransactions ct
                     LEFT JOIN Customers c ON c.Id = ct.CustomerId
                     LEFT JOIN Sales s ON s.Id = ct.SaleId
                     WHERE ct.Type = 'Payment'";
        if (from.HasValue) sql += " AND date(ct.CreatedAt) >= date(@from)";
        if (to.HasValue) sql += " AND date(ct.CreatedAt) <= date(@to)";
        if (!string.IsNullOrEmpty(paymentMethod)) sql += " AND ct.PaymentMethod = @pm";
        if (!string.IsNullOrEmpty(customerKeyword)) sql += " AND (c.Name LIKE @kw OR c.Phone LIKE @kw)";
        sql += " ORDER BY ct.CreatedAt DESC";

        using var cmd = new SQLiteCommand(sql, conn);
        if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd"));
        if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrEmpty(paymentMethod)) cmd.Parameters.AddWithValue("@pm", paymentMethod);
        if (!string.IsNullOrEmpty(customerKeyword)) cmd.Parameters.AddWithValue("@kw", $"%{customerKeyword}%");
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapTransaction(rdr));
        }
        return list;
    }

    public static (decimal TotalPayments, decimal TotalCash, decimal TotalEWallet, decimal TotalOther) GetPaymentSummary(DateTime? from = null, DateTime? to = null)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var where = "WHERE Type = 'Payment'";
        if (from.HasValue) where += " AND date(CreatedAt) >= date(@from)";
        if (to.HasValue) where += " AND date(CreatedAt) <= date(@to)";

        var sql = $"SELECT COALESCE(SUM(Credit), 0) as Total, " +
                  $"COALESCE(SUM(CASE WHEN PaymentMethod = 'Cash' THEN Credit ELSE 0 END), 0) as Cash, " +
                  $"COALESCE(SUM(CASE WHEN PaymentMethod = 'E-Wallet' THEN Credit ELSE 0 END), 0) as EWallet " +
                  $"FROM CreditTransactions {where}";

        using var cmd = new SQLiteCommand(sql, conn);
        if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd"));
        if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd"));
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
        {
            var total = Convert.ToDecimal(rdr["Total"]);
            var cash = Convert.ToDecimal(rdr["Cash"]);
            var ewallet = Convert.ToDecimal(rdr["EWallet"]);
            return (total, cash, ewallet, total - cash - ewallet);
        }
        return (0, 0, 0, 0);
    }

    public static List<CreditTransaction> GetAllUnpaid()
    {
        var list = new List<CreditTransaction>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT ct.*, c.Name as CustomerName, s.InvoiceNo
                     FROM CreditTransactions ct
                     LEFT JOIN Customers c ON c.Id = ct.CustomerId
                     LEFT JOIN Sales s ON s.Id = ct.SaleId
                     WHERE ct.Type = 'Sale' AND ct.Balance > 0
                     ORDER BY ct.CreatedAt ASC";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapTransaction(rdr));
        }
        return list;
    }

    public static (decimal Current, decimal D30, decimal D60, decimal D90, decimal D90Plus) GetAgingSummary(int? customerId = null)
    {
        var unpaid = GetAllUnpaid();
        if (customerId.HasValue) unpaid = unpaid.Where(t => t.CustomerId == customerId.Value).ToList();

        decimal current = 0, d30 = 0, d60 = 0, d90 = 0, d90Plus = 0;
        var now = DateTime.Now;
        foreach (var t in unpaid)
        {
            var days = (now - ParseDate(t.CreatedAt)).Days;
            var bal = t.Balance;
            if (days <= 0) current += bal;
            else if (days <= 30) d30 += bal;
            else if (days <= 60) d60 += bal;
            else if (days <= 90) d90 += bal;
            else d90Plus += bal;
        }
        return (current, d30, d60, d90, d90Plus);
    }

    public static decimal GetTotalReceivables()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT COALESCE(SUM(CreditBalance), 0) FROM Customers WHERE CreditBalance > 0";
        using var cmd = new SQLiteCommand(sql, conn);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    public static List<(Customer Customer, decimal Balance)> GetCustomersWithCredit()
    {
        var list = new List<(Customer, decimal)>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers WHERE CreditBalance > 0 ORDER BY CreditBalance DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var c = MapCustomer(rdr);
            list.Add((c, c.CreditBalance));
        }
        return list;
    }

    public static List<Customer> GetAllDebtCustomers()
    {
        var list = new List<Customer>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers WHERE CreditBalance > 0 ORDER BY CreditBalance DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(MapCustomer(rdr));
        return list;
    }

    public static List<(Customer Customer, decimal TodayBalance)> GetTodayDebtCustomers()
    {
        var list = new List<(Customer, decimal)>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"
            SELECT c.*, COALESCE(SUM(ct.Debit) - SUM(ct.Credit), 0) as TodayBalance
            FROM Customers c
            INNER JOIN CreditTransactions ct ON ct.CustomerId = c.Id
            WHERE date(ct.CreatedAt) = date('now','localtime')
            GROUP BY c.Id
            HAVING TodayBalance > 0
            ORDER BY TodayBalance DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var c = MapCustomer(rdr);
            list.Add((c, Convert.ToDecimal(rdr["TodayBalance"])));
        }
        return list;
    }

    public static List<Customer> GetCustomersWithCreditBalance()
    {
        var list = new List<Customer>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers ORDER BY Name";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(MapCustomer(rdr));
        return list;
    }

    public static List<Customer> SearchCreditCustomers(string keyword)
    {
        var list = new List<Customer>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers WHERE CreditBalance > 0 AND (Name LIKE @kw OR Phone LIKE @kw) ORDER BY CreditBalance DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(MapCustomer(rdr));
        return list;
    }

    public static bool CheckCreditLimit(int customerId, decimal amount, out string warning)
    {
        warning = "";
        var customer = CustomerService.GetById(customerId);
        if (customer == null || !customer.HasCreditLimit) return true;

        var projected = customer.CreditBalance + amount;
        if (projected > customer.CreditLimit)
        {
            warning = $"Credit limit exceeded!\nLimit: \u20b1{customer.CreditLimit:N2}\nCurrent: \u20b1{customer.CreditBalance:N2}\nThis sale: \u20b1{amount:N2}\nProjected: \u20b1{projected:N2}";
            return false;
        }
        return true;
    }

    public static string GenerateStatement(int customerId, DateTime? from = null, DateTime? to = null)
    {
        var customer = CustomerService.GetById(customerId);
        if (customer == null) return "";

        var txns = GetByCustomer(customerId, from, to);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"STATEMENT OF ACCOUNT");
        sb.AppendLine($"Customer: {customer.Name}");
        sb.AppendLine($"Phone: {customer.Phone}");
        sb.AppendLine($"Address: {customer.Address}");
        sb.AppendLine($"Period: {(from?.ToString("yyyy-MM-dd") ?? "Start")} to {(to?.ToString("yyyy-MM-dd") ?? "Present")}");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine(new string('-', 80));
        sb.AppendLine($"{ "Date",-20} {"Type",-12} {"Description",-25} {"Debit",12} {"Credit",12} {"Balance",12}");
        sb.AppendLine(new string('-', 80));

        foreach (var t in txns.OrderBy(x => x.CreatedAt))
        {
            sb.AppendLine($"{t.CreatedAt,-20} {t.Type,-12} {t.Description,-25} {t.Debit,12:N2} {t.Credit,12:N2} {t.Balance,12:N2}");
        }

        sb.AppendLine(new string('-', 80));
        sb.AppendLine($"Current Balance: \u20b1{customer.CreditBalance:N2}");
        if (customer.HasCreditLimit)
        {
            sb.AppendLine($"Credit Limit: \u20b1{customer.CreditLimit:N2}");
            sb.AppendLine($"Available Credit: \u20b1{customer.AvailableCredit:N2}");
        }

        return sb.ToString();
    }

    private static CreditTransaction MapTransaction(SQLiteDataReader rdr)
    {
        var createdAt = rdr["CreatedAt"].ToString() ?? "";
        var days = (DateTime.Now - ParseDate(createdAt)).Days;
        return new CreditTransaction
        {
            Id = Convert.ToInt32(rdr["Id"]),
            CustomerId = Convert.ToInt32(rdr["CustomerId"]),
            CustomerName = rdr["CustomerName"]?.ToString() ?? "",
            SaleId = rdr["SaleId"] != DBNull.Value ? Convert.ToInt32(rdr["SaleId"]) : null,
            InvoiceNo = rdr["InvoiceNo"]?.ToString() ?? "",
            Type = rdr["Type"].ToString() ?? "",
            Description = rdr["Description"].ToString() ?? "",
            Debit = Convert.ToDecimal(rdr["Debit"]),
            Credit = Convert.ToDecimal(rdr["Credit"]),
            Balance = Convert.ToDecimal(rdr["Balance"]),
            PaymentMethod = rdr["PaymentMethod"]?.ToString() ?? "",
            ReferenceNo = rdr["ReferenceNo"]?.ToString() ?? "",
            CreatedAt = createdAt,
            AgingDays = days
        };
    }

    private static Customer MapCustomer(SQLiteDataReader rdr)
    {
        return new Customer
        {
            Id = Convert.ToInt32(rdr["Id"]),
            Name = rdr["Name"].ToString() ?? "",
            Phone = rdr["Phone"].ToString() ?? "",
            Email = rdr["Email"].ToString() ?? "",
            Address = rdr["Address"]?.ToString() ?? "",
            LoyaltyPoints = Convert.ToInt32(rdr["LoyaltyPoints"]),
            CreditBalance = rdr["CreditBalance"] != DBNull.Value ? Convert.ToDecimal(rdr["CreditBalance"]) : 0,
            CreditLimit = rdr["CreditLimit"] != DBNull.Value ? Convert.ToDecimal(rdr["CreditLimit"]) : 0,
            CreatedAt = DateTime.SpecifyKind(DateTime.Parse(rdr["CreatedAt"].ToString()!), DateTimeKind.Local)
        };
    }

    private static DateTime ParseDate(string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var dt)) return dt;
        return DateTime.Now;
    }
}

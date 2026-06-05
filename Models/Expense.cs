namespace JumongPosV1._01.Models;

public class Expense
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ReferenceNo { get; set; }
    public string CashierUsername { get; set; } = "";
    public string ReceiptImage { get; set; } = "";
    public string Timestamp { get; set; } = "";
}

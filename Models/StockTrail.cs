namespace JumongPosV1._01.Models;

public class StockTrail
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal QuantityAdded { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    public string Reference { get; set; } = "";
    public string InvoiceNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public int Synced { get; set; } = 0;
}
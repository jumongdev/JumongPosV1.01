namespace JumongPosV1._01.Models;

public class VoidLog
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int? SaleItemId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string InvoiceNo { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

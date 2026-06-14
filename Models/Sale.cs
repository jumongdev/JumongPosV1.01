using JumongPosV1._01.Services;

namespace JumongPosV1._01.Models;

public class Sale
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; } = TimeHelper.Now;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Change { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public int? CustomerId { get; set; }
    public int? UserId { get; set; }
    public string OrderType { get; set; } = "Walk-in";
    public string CustomerName { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
    public decimal CashPaid { get; set; }
    public decimal EwPaid { get; set; }
    public string CashierName { get; set; } = "";
    public bool IsVoided { get; set; }
    public string? VoidedAt { get; set; }
    public bool Synced { get; set; }
    public decimal EffectiveTotal { get; set; }
    public List<SaleItem> Items { get; set; } = new();

    public string Status => IsVoided ? "VOIDED" : "OK";
}

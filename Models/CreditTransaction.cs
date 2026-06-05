namespace JumongPosV1._01.Models;

public class CreditTransaction
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int? SaleId { get; set; }
    public string InvoiceNo { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public int AgingDays { get; set; }
    public string AgingBucket
    {
        get
        {
            if (AgingDays <= 0) return "Current";
            if (AgingDays <= 30) return "1-30 days";
            if (AgingDays <= 60) return "31-60 days";
            if (AgingDays <= 90) return "61-90 days";
            return "90+ days";
        }
    }
}
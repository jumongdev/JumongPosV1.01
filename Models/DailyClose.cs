namespace JumongPosV1._01.Models;

public class DailyClose
{
    public int Id { get; set; }
    public string CloseDate { get; set; } = "";
    public decimal TotalSales { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalEWallet { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal TotalVoided { get; set; }
    public decimal TotalExpenses { get; set; }
    public int Denom1000 { get; set; }
    public int Denom500 { get; set; }
    public int Denom200 { get; set; }
    public int Denom100 { get; set; }
    public int Denom50 { get; set; }
    public int Denom20 { get; set; }
    public decimal DenomCoins { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal Difference { get; set; }
    public string Notes { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
}
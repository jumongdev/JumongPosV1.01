namespace JumongPosV1._01.Models;

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string UnitName { get; set; } = "";
    public int QtyPerUnit { get; set; } = 1;
    public decimal UnitCost { get; set; }
    public bool IsVoided { get; set; }
}

namespace JumongPosV1._01.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int StockQty { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string ModifiedBy { get; set; } = string.Empty;

    public string DisplayWithStock => $"{Name}  —  Stock: {StockQty} pcs";
    public string DisplayWithPrice => $"{Name}  [{Barcode}]  \u20b1{Price:N2}  |  Stock: {StockQty}";
}

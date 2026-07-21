namespace JumongPosV1._01.Models;

public class InventoryCount
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    public int ProductId { get; set; }
    public string Barcode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int SystemQty { get; set; }
    public int ActualQty { get; set; }
    public int Variance => ActualQty - SystemQty;
    public string CountedBy { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool Adjusted { get; set; }
}

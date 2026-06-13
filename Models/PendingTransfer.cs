namespace JumongPosV1._01.Models;

public class PendingTransfer
{
    public int OrderId { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string Notes { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TransferItem> Items { get; set; } = new();
}

public class TransferItem
{
    public string ProductName { get; set; } = "";
    public int BaseQty { get; set; }
    public string BaseUnitName { get; set; } = "Piece";
    public string Barcode { get; set; } = "";
    public int MasterProductId { get; set; }
}

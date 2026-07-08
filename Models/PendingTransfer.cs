namespace JumongPosV1._01.Models;

public class PendingTransfer
{
    public int OrderId { get; set; }
    public string ClientName { get; set; } = "";
    public string ItemsSummary { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<TransferItem> Items { get; set; } = new();
}

public class TransferItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int BaseQty { get; set; }
    public string BaseUnitName { get; set; } = "Piece";
    public string Barcode { get; set; } = "";
    public int MasterProductId { get; set; }
}

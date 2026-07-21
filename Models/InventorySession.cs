namespace JumongPosV1._01.Models;

public class InventorySession
{
    public string SessionId { get; set; } = "";
    public string CountedBy { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string? EndedAt { get; set; }
    public string Status { get; set; } = "Active";
    public int TotalItems { get; set; }
    public int ItemsWithVariance { get; set; }
}

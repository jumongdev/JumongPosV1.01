using System.Text.Json;

namespace JumongPosV1._01.Models;

public class HeldCart
{
    public int Id { get; set; }
    public string OrderType { get; set; } = "Walk-in";
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string ItemsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<SaleItem> DeserializeItems()
    {
        return JsonSerializer.Deserialize<List<SaleItem>>(ItemsJson) ?? new();
    }

    public static string SerializeItems(List<SaleItem> items)
    {
        return JsonSerializer.Serialize(items);
    }

    public string Summary => $"[{OrderType}] {CustomerName}  -  {CreatedAt:MM/dd HH:mm}";
}

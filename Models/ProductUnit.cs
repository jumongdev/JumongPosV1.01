namespace JumongPosV1._01.Models;

public class ProductUnit
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string UnitName { get; set; } = "Piece";
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int QtyPerUnit { get; set; } = 1;
    public bool IsDefault { get; set; }
    public int PointsPerUnit { get; set; }
}

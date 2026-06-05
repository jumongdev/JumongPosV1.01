namespace JumongPosV1._01.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int LoyaltyPoints { get; set; }
    public decimal CreditBalance { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string ModifiedBy { get; set; } = string.Empty;

    public string DisplayPhone => string.IsNullOrEmpty(Phone) || Phone.StartsWith("imported-") ? "—" : Phone;
    public string DisplayName => $"{Name}  |  {DisplayPhone}";
    public string DisplayWithCredit => $"{Name}  |  {DisplayPhone}  (Credit: \u20b1{CreditBalance:N2})";
    public decimal AvailableCredit => CreditLimit > 0 ? CreditLimit - CreditBalance : -1;
    public bool HasCreditLimit => CreditLimit > 0;
    public bool IsOverLimit => HasCreditLimit && CreditBalance > CreditLimit;
}

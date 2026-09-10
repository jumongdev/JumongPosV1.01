namespace JumongPosV1._01.Models;

// Cloud e-commerce chat payloads (shop_chat_* tables) — para sa HQ POS messenger
public class CloudChatConversation
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public string LastSender { get; set; } = "";
    public DateTime? LastAt { get; set; }
    public string Phone { get; set; } = "";
    public string Subdivision { get; set; } = "";
    public string Block { get; set; } = "";
    public string Lot { get; set; } = "";
    public string Address { get; set; } = "";
    public int Unread { get; set; }
}

public class CloudChatMessage
{
    public long Id { get; set; }
    public string Sender { get; set; } = "";
    public string Message { get; set; } = "";
    public bool SeenByAdmin { get; set; }
    public string ReplyBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

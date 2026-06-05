namespace JumongPosV1._01.Models;

public class PendingEmail
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsHtml { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.Now;
}

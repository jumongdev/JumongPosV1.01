namespace JumongPosV1._01.Models;

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = "";
    public string SettingKey { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    public string UserName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

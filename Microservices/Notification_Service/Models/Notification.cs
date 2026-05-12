namespace Notification_Service.Models;

public class Notification
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int Level { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

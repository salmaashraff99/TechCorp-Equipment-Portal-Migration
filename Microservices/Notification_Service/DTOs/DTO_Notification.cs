namespace Notification_Service.DTOs;

public class DTO_SendNotification
{
    public int RequestId { get; set; }
    public int ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int Level { get; set; }
    public DateTime SentAt { get; set; }
}

public class DTO_NotificationResponse
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int Level { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

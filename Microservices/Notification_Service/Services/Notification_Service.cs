using System.Net.Mail;
using Notification_Service.DTOs;
using Notification_Service.Infrastructure;
using Notification_Service.Models;
using TechCorp.Shared.DTOs;
using TechCorp.Shared.Enums;

namespace Notification_Service.Services;

public class NotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IUnitOfWork uow, IConfiguration config, ILogger<NotificationService> logger)
    {
        _uow    = uow;
        _config = config;
        _logger = logger;
    }

    public async Task<DTO_Response<string>> SendAsync(DTO_SendNotification dto)
    {
        var subject = BuildSubject(dto);
        var body    = BuildBody(dto);

        var notification = new Notification
        {
            RequestId      = dto.RequestId,
            ActorId        = dto.ActorId,
            Action         = dto.Action,
            Level          = dto.Level,
            RecipientEmail = ResolveRecipient(dto),
            Subject        = subject,
            Body           = body,
            CreatedAt      = DateTime.UtcNow
        };

        await _uow.Notifications.AddAsync(notification);
        await _uow.SaveAsync();

        var sent = await DispatchEmailAsync(notification);

        if (sent)
        {
            notification.IsSent = true;
            notification.SentAt = DateTime.UtcNow;
        }
        else
        {
            notification.RetryCount++;
            notification.ErrorMessage = "SMTP delivery failed.";
        }

        _uow.Notifications.Update(notification);
        await _uow.SaveAsync();

        return sent
            ? Ok<string>("Notification sent.")
            : Fail<string>((int)StatusCode.InternalServerError, "Notification queued for retry.");
    }

    public async Task<DTO_Response<DTO_NotificationResponse>> GetByRequestIdAsync(int requestId)
    {
        var notifications = await _uow.Notifications.GetByRequestIdAsync(requestId);
        var data = notifications.Select(Map).ToList();
        return Ok(data);
    }

    public async Task<DTO_Response<string>> RetryFailedAsync()
    {
        var failed = (await _uow.Notifications.GetFailedAsync()).ToList();
        int retried = 0;

        foreach (var n in failed)
        {
            var sent = await DispatchEmailAsync(n);
            n.RetryCount++;
            if (sent)
            {
                n.IsSent  = true;
                n.SentAt  = DateTime.UtcNow;
                retried++;
            }
            else
            {
                n.ErrorMessage = $"Retry {n.RetryCount} failed.";
            }
            _uow.Notifications.Update(n);
        }

        await _uow.SaveAsync();
        return Ok<string>($"Retried {failed.Count} notifications. {retried} succeeded.");
    }

    private async Task<bool> DispatchEmailAsync(Notification n)
    {
        try
        {
            var smtpHost = _config["Smtp:Host"] ?? "localhost";
            var smtpPort = int.Parse(_config["Smtp:Port"] ?? "25");
            var fromAddr = _config["Smtp:From"] ?? "no-reply@techcorp-int.com";

            using var mail = new MailMessage(fromAddr, n.RecipientEmail, n.Subject, n.Body) { IsBodyHtml = true };
            using var smtp = new SmtpClient(smtpHost, smtpPort);
            await smtp.SendMailAsync(mail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email for notification {Id}", n.Id);
            return false;
        }
    }

    private static string BuildSubject(DTO_SendNotification dto) =>
        dto.Action switch
        {
            "Approved" => $"Equipment Request #{dto.RequestId} Approved at Level {dto.Level}",
            "Rejected" => $"Equipment Request #{dto.RequestId} Rejected at Level {dto.Level}",
            _          => $"Equipment Request #{dto.RequestId} Update"
        };

    private static string BuildBody(DTO_SendNotification dto) =>
        $"<html><body><p>Equipment request <b>#{dto.RequestId}</b> has been <b>{dto.Action}</b> " +
        $"at approval level {dto.Level}.</p>" +
        $"<p>Action taken by employee #{dto.ActorId} at {dto.SentAt:dd/MM/yyyy HH:mm} UTC.</p>" +
        $"<p>Please log in to the TechCorp Equipment Portal to view details.</p></body></html>";

    private static string ResolveRecipient(DTO_SendNotification dto) =>
        // In production this would look up the correct recipient from a user service
        $"notify-request-{dto.RequestId}@techcorp-int.com";

    private static DTO_NotificationResponse Map(Notification n) => new()
    {
        Id             = n.Id,
        RequestId      = n.RequestId,
        Action         = n.Action,
        Level          = n.Level,
        RecipientEmail = n.RecipientEmail,
        IsSent         = n.IsSent,
        RetryCount     = n.RetryCount,
        CreatedAt      = n.CreatedAt,
        SentAt         = n.SentAt,
        ErrorMessage   = n.ErrorMessage
    };

    private static DTO_Response<T> Ok<T>(string message) => new()
    {
        code = (int)StatusCode.OK, error = false, message = message, data = new List<T>()
    };

    private static DTO_Response<T> Ok<T>(List<T> data) => new()
    {
        code = (int)StatusCode.OK, error = false, message = "Success", data = data
    };

    private static DTO_Response<T> Fail<T>(int code, string message) => new()
    {
        code = code, error = true, message = message, data = new List<T>()
    };
}

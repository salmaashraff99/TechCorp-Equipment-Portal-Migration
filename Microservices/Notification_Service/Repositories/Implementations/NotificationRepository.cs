using Microsoft.EntityFrameworkCore;
using Notification_Service.Infrastructure.Data;
using Notification_Service.Models;
using Notification_Service.Repositories.Interfaces;

namespace Notification_Service.Repositories.Implementations;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;

    public NotificationRepository(NotificationDbContext context) => _context = context;

    public async Task<Notification?> GetByIdAsync(int id) =>
        await _context.Notifications.FindAsync(id);

    public async Task<IEnumerable<Notification>> GetByRequestIdAsync(int requestId) =>
        await _context.Notifications
            .Where(n => n.RequestId == requestId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Notification>> GetFailedAsync(int maxRetries = 3) =>
        await _context.Notifications
            .Where(n => !n.IsSent && n.RetryCount < maxRetries)
            .ToListAsync();

    public async Task AddAsync(Notification notification) =>
        await _context.Notifications.AddAsync(notification);

    public void Update(Notification notification) =>
        _context.Notifications.Update(notification);
}

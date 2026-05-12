using Notification_Service.Infrastructure.Data;
using Notification_Service.Repositories.Interfaces;

namespace Notification_Service.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly NotificationDbContext _context;

    public INotificationRepository Notifications { get; }

    public UnitOfWork(NotificationDbContext context, INotificationRepository notifications)
    {
        _context      = context;
        Notifications = notifications;
    }

    public async Task<int> SaveAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}

using Notification_Service.Repositories.Interfaces;

namespace Notification_Service.Infrastructure;

public interface IUnitOfWork : IDisposable
{
    INotificationRepository Notifications { get; }
    Task<int> SaveAsync();
}

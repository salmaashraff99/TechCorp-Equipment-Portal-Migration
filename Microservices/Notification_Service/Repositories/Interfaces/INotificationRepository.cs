using Notification_Service.Models;

namespace Notification_Service.Repositories.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(int id);
    Task<IEnumerable<Notification>> GetByRequestIdAsync(int requestId);
    Task<IEnumerable<Notification>> GetFailedAsync(int maxRetries = 3);
    Task AddAsync(Notification notification);
    void Update(Notification notification);
}

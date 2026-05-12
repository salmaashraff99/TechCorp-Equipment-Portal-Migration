using Microsoft.EntityFrameworkCore;
using Notification_Service.Models;

namespace Notification_Service.Infrastructure.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestId);
            e.HasIndex(x => new { x.IsSent, x.RetryCount });
        });
    }
}

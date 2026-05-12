using Equipment_Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Equipment_Service.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<EquipmentRequest> EquipmentRequests => Set<EquipmentRequest>();
    public DbSet<EquipmentRequestItem> EquipmentRequestItems => Set<EquipmentRequestItem>();
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WorkFlowStep> WorkFlowSteps => Set<WorkFlowStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipmentRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EstimatedCost).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.EquipmentType).WithMany().HasForeignKey(x => x.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.WorkFlowSteps).WithOne(x => x.Request).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EquipmentRequestItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Ignore(x => x.TotalPrice);
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<EquipmentType>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AveragePrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.MinPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.MaxPrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<WorkFlowStep>(e =>
        {
            e.HasKey(x => x.Id);
        });
    }
}

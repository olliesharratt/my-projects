using AssetMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssetMonitoring.Infrastructure.Persistence;

public sealed class MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>().HasIndex(x => new { x.Site, x.Type, x.Status });
        modelBuilder.Entity<TelemetryReading>().HasIndex(x => new { x.AssetId, x.RecordedAt });
        modelBuilder.Entity<Alert>().HasIndex(x => new { x.AssetId, x.CreatedAt });
        modelBuilder.Entity<WorkOrder>().HasOne(x => x.Asset).WithMany(x => x.WorkOrders).HasForeignKey(x => x.AssetId);
    }
}

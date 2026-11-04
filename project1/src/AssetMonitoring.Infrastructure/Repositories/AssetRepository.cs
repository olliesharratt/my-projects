using AssetMonitoring.Domain.Entities;
using AssetMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMonitoring.Infrastructure.Repositories;

public sealed class AssetRepository(MonitoringDbContext db)
{
    public IQueryable<Asset> Query() => db.Assets.AsNoTracking();
    public Task<Asset?> FindAsync(Guid id, CancellationToken ct) => db.Assets.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public MonitoringDbContext Db => db;
}

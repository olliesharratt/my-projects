using AssetMonitoring.Api.Hubs;
using AssetMonitoring.Domain.Entities;
using AssetMonitoring.Domain.Enums;
using AssetMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AssetMonitoring.Api.Services;

public sealed class TelemetryWorker(IServiceScopeFactory scopes, IHubContext<MonitoringHub> hub, IConfiguration configuration, ILogger<TelemetryWorker> logger) : BackgroundService
{
    private readonly Random random = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(configuration.GetValue("Telemetry:IntervalSeconds", 5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
                var assets = await db.Assets.ToListAsync(stoppingToken);
                foreach (var asset in assets)
                {
                    var reading = new TelemetryReading { Asset = asset, AssetId = asset.Id, Temperature = decimal.Round(55 + (decimal)random.NextDouble() * 35, 2), Vibration = decimal.Round((decimal)random.NextDouble() * 8, 2), Pressure = decimal.Round(2 + (decimal)random.NextDouble() * 5, 2) };
                    db.TelemetryReadings.Add(reading);
                    asset.UpdatedAt = DateTimeOffset.UtcNow;
                    if (reading.Temperature > 85 || reading.Vibration > 7) { asset.Status = AssetStatus.Warning; db.Alerts.Add(new Alert { Asset = asset, AssetId = asset.Id, Metric = reading.Temperature > 85 ? "Temperature" : "Vibration", Value = reading.Temperature > 85 ? reading.Temperature : reading.Vibration, Threshold = reading.Temperature > 85 ? 85 : 7 }); }
                    await hub.Clients.All.SendAsync("telemetryUpdated", new { assetId = asset.Id, reading.RecordedAt, reading.Temperature, reading.Vibration, reading.Pressure }, stoppingToken);
                }
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception) { logger.LogError(exception, "Telemetry cycle failed"); }
            await Task.Delay(delay, stoppingToken);
        }
    }
}

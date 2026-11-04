using AssetMonitoring.Domain.Enums;

namespace AssetMonitoring.Domain.Entities;

public sealed class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Site { get; set; }
    public required string Type { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Online;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<TelemetryReading> Telemetry { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];
    public ICollection<WorkOrder> WorkOrders { get; set; } = [];
}

public sealed class TelemetryReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public required Asset Asset { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal Temperature { get; set; }
    public decimal Vibration { get; set; }
    public decimal Pressure { get; set; }
}

public sealed class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public required Asset Asset { get; set; }
    public required string Metric { get; set; }
    public decimal Value { get; set; }
    public decimal Threshold { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public required Asset Asset { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;
    public string? AssignedTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

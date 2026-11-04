namespace AssetMonitoring.Application.Telemetry;

public sealed record TelemetryDto(Guid AssetId, DateTimeOffset RecordedAt, decimal Temperature, decimal Vibration, decimal Pressure);
public sealed record AlertDto(Guid Id, Guid AssetId, string Metric, decimal Value, decimal Threshold, bool IsAcknowledged, DateTimeOffset CreatedAt);

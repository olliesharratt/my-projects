namespace ReportTool;

/// <summary>A CSV row exactly as read: everything is a string so bad data can be reported, not crash the parser.</summary>
public class RawRow
{
    public string? Sku { get; set; }
    public string? Site { get; set; }
    public string? Quantity { get; set; }
    public string? UnitCost { get; set; }
    public string? ReportDate { get; set; }
}

public record StockRow(string Sku, string Site, int Quantity, decimal UnitCost, DateOnly ReportDate);
public record RowError(int Line, string Field, string Reason);
public record ImportResult(bool AlreadyImported, int Imported, IReadOnlyList<RowError> Errors);
public record SiteSummary(string Site, int Items, long Units, decimal Value);

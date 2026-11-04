using System.Globalization;

namespace ReportTool;

public static class RowValidator
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy"];

    public static (StockRow? Row, List<RowError> Errors) Validate(int line, RawRow r)
    {
        var errors = new List<RowError>();
        var sku = r.Sku?.Trim() ?? "";
        var site = r.Site?.Trim() ?? "";
        if (sku.Length == 0) errors.Add(new(line, "Sku", "Required"));
        if (site.Length == 0) errors.Add(new(line, "Site", "Required"));

        if (!int.TryParse(r.Quantity?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty < 0)
            errors.Add(new(line, "Quantity", "Must be a non-negative whole number"));

        if (!decimal.TryParse(r.UnitCost?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) || cost < 0)
            errors.Add(new(line, "UnitCost", "Must be a non-negative number"));

        if (!DateOnly.TryParseExact(r.ReportDate?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            errors.Add(new(line, "ReportDate", "Use yyyy-MM-dd or dd/MM/yyyy"));

        return errors.Count == 0 ? (new StockRow(sku, site, qty, cost, date), errors) : (null, errors);
    }
}

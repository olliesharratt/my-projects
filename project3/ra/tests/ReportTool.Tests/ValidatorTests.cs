using ReportTool;
using Xunit;

public class ValidatorTests
{
    [Fact]
    public void ValidRow_ParsesBothDateFormats()
    {
        var (a, _) = RowValidator.Validate(2, new RawRow { Sku = "A1", Site = "Cramlington", Quantity = "5", UnitCost = "2.50", ReportDate = "2026-10-19" });
        var (b, _) = RowValidator.Validate(3, new RawRow { Sku = "A1", Site = "Cramlington", Quantity = "5", UnitCost = "2.50", ReportDate = "19/10/2026" });
        Assert.Equal(a!.ReportDate, b!.ReportDate);
    }

    [Theory]
    [InlineData("", "S", "1", "1", "2026-01-01", "Sku")]
    [InlineData("A", "S", "-1", "1", "2026-01-01", "Quantity")]
    [InlineData("A", "S", "1.5", "1", "2026-01-01", "Quantity")]
    [InlineData("A", "S", "1", "abc", "2026-01-01", "UnitCost")]
    [InlineData("A", "S", "1", "1", "31/31/2026", "ReportDate")]
    public void InvalidRow_ReportsTheRightField(string sku, string site, string qty, string cost, string date, string field)
    {
        var (row, errors) = RowValidator.Validate(7, new RawRow { Sku = sku, Site = site, Quantity = qty, UnitCost = cost, ReportDate = date });
        Assert.Null(row);
        Assert.Contains(errors, e => e.Field == field && e.Line == 7);
    }
}

using Microsoft.Data.Sqlite;
using ReportTool;
using Xunit;

public class ImportTests
{
    private static (ImportService Svc, SqliteConnection Db) Create()
    {
        var db = new SqliteConnection("Data Source=:memory:");
        db.Open();
        var svc = new ImportService(db);
        svc.Init();
        return (svc, db);
    }

    private static string Csv(string body)
    {
        var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(p, "Sku,Site,Quantity,Unit Cost,Report Date\n" + body);
        return p;
    }

    [Fact]
    public void ImportingSameFileTwice_IsSkipped()
    {
        var (svc, db) = Create();
        using var _ = db;
        var f = Csv("A1,Cramlington,10,2.00,2026-10-19\n");
        Assert.Equal(1, svc.Import(f).Imported);
        Assert.True(svc.Import(f).AlreadyImported);
        Assert.Single(svc.Summary());
    }

    [Fact]
    public void BadRows_AreRejectedWithLineNumbers_GoodRowsStillLoad()
    {
        var (svc, db) = Create();
        using var _ = db;
        var r = svc.Import(Csv("A1,Cramlington,10,2.00,2026-10-19\nB2,Aberdeen,-4,1.00,2026-10-19\n"));
        Assert.Equal(1, r.Imported);
        Assert.Contains(r.Errors, e => e.Line == 3 && e.Field == "Quantity");
    }
}

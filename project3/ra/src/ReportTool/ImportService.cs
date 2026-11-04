using System.Globalization;
using System.Security.Cryptography;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;

namespace ReportTool;

public class ImportService(SqliteConnection db)
{
    public void Init() => Exec("""
        CREATE TABLE IF NOT EXISTS imports (hash TEXT PRIMARY KEY, file TEXT, imported_at TEXT, rows INTEGER);
        CREATE TABLE IF NOT EXISTS stock (sku TEXT, site TEXT, quantity INTEGER, unit_cost REAL, report_date TEXT, import_hash TEXT);
        """);

    /// <summary>Idempotent: a file with the same content hash is never loaded twice.</summary>
    public ImportResult Import(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        using (var check = db.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM imports WHERE hash = $h";
            check.Parameters.AddWithValue("$h", hash);
            if ((long)check.ExecuteScalar()! > 0) return new(true, 0, []);
        }

        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = a => a.Header.Replace(" ", "").ToLowerInvariant(),
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null
        };
        using var csv = new CsvReader(new StreamReader(new MemoryStream(bytes)), cfg);

        var good = new List<StockRow>();
        var errors = new List<RowError>();
        var line = 1; // header is line 1
        foreach (var raw in csv.GetRecords<RawRow>())
        {
            line++;
            var (row, errs) = RowValidator.Validate(line, raw);
            if (row is not null) good.Add(row);
            errors.AddRange(errs);
        }

        using var tx = db.BeginTransaction();
        foreach (var r in good)
        {
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO stock VALUES ($s,$site,$q,$c,$d,$h)";
            cmd.Parameters.AddWithValue("$s", r.Sku);
            cmd.Parameters.AddWithValue("$site", r.Site);
            cmd.Parameters.AddWithValue("$q", r.Quantity);
            cmd.Parameters.AddWithValue("$c", (double)r.UnitCost);
            cmd.Parameters.AddWithValue("$d", r.ReportDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.ExecuteNonQuery();
        }
        using (var rec = db.CreateCommand())
        {
            rec.Transaction = tx;
            rec.CommandText = "INSERT INTO imports VALUES ($h,$f,$t,$n)";
            rec.Parameters.AddWithValue("$h", hash);
            rec.Parameters.AddWithValue("$f", Path.GetFileName(path));
            rec.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            rec.Parameters.AddWithValue("$n", good.Count);
            rec.ExecuteNonQuery();
        }
        tx.Commit();
        return new(false, good.Count, errors);
    }

    public List<SiteSummary> Summary()
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT site, COUNT(*), SUM(quantity), SUM(quantity * unit_cost) FROM stock GROUP BY site ORDER BY site";
        using var rd = cmd.ExecuteReader();
        var list = new List<SiteSummary>();
        while (rd.Read()) list.Add(new(rd.GetString(0), rd.GetInt32(1), rd.GetInt64(2), (decimal)rd.GetDouble(3)));
        return list;
    }

    private void Exec(string sql) { using var c = db.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); }
}

using System.Globalization;
using CsvHelper;
using Microsoft.Data.Sqlite;
using ReportTool;

if (args.Length < 1) { Console.Error.WriteLine("Usage: ReportTool <file.csv> [--db reports.db]"); return 64; }
var file = args[0];
var dbPath = args.Length >= 3 && args[1] == "--db" ? args[2] : "reports.db";

using var db = new SqliteConnection($"Data Source={dbPath}");
db.Open();
var svc = new ImportService(db);
svc.Init();

var result = svc.Import(file);
if (result.AlreadyImported) { Console.WriteLine($"Skipped: {Path.GetFileName(file)} was already imported."); return 0; }

Console.WriteLine($"Imported {result.Imported} rows, rejected {result.Errors.Select(e => e.Line).Distinct().Count()}.");
if (result.Errors.Count > 0)
{
    var errPath = Path.ChangeExtension(file, ".errors.csv");
    using var w = new StreamWriter(errPath);
    using var csv = new CsvWriter(w, CultureInfo.InvariantCulture);
    csv.WriteRecords(result.Errors);
    Console.WriteLine($"Error report: {errPath}");
}

Console.WriteLine("\n| Site | Items | Units | Value |\n|---|---|---|---|");
foreach (var s in svc.Summary()) Console.WriteLine($"| {s.Site} | {s.Items} | {s.Units} | {s.Value:N2} |");
return result.Errors.Count > 0 ? 2 : 0; // non-zero exit lets a scheduler flag dirty files

# Reporting Automation Tool

Validates weekly stock and project CSV reports, imports valid rows into SQLite, writes rejected rows to an error report, and prints a per-site summary.

**Stack:** C# / .NET 8, CsvHelper, SQLite, xUnit, Docker, GitHub Actions.

## Run

Prerequisite: .NET 8 SDK.

From this directory:
```bash
dotnet run --project src/ReportTool -- sample-data/week42.csv --db reports.db
dotnet test tests/ReportTool.Tests
```

The sample intentionally contains both valid and invalid rows. A run imports three rows, rejects three, writes `sample-data/week42.errors.csv`, and exits with code `2` to signal that the input needs attention.

## Sample Output

```text
Imported 3 rows, rejected 3.
Error report: sample-data/week42.errors.csv

| Site | Items | Units | Value |
|---|---|---|---|
| Aberdeen | 2 | 312 | 12,105.00 |
| Cramlington | 1 | 40 | 5,020.00 |
```

Run the same command again with the same database to see the idempotency check. Use a disposable database path when experimenting:
```bash
dotnet run --project src/ReportTool -- sample-data/week42.csv --db /tmp/reports.db
```

Build and run the Docker image:
```bash
docker build -t report-tool .
docker run --rm -v "$PWD/sample-data:/data" report-tool /data/week42.csv
```

## Design and Quality

- Per-row validation covers required fields, numeric ranges, and two date formats; errors include source line numbers.
- SHA-256 content hashes prevent duplicate imports; valid rows load transactionally.
- CI runs tests with coverage on pushes to `main` and pull requests. A passing `main` build publishes a Docker image to GitHub Container Registry.
- CodeQL runs for pushes to `main`, pull requests, and on a weekly schedule; Dependabot checks NuGet, GitHub Actions, and Docker dependencies weekly.

See [engineering-process.md](docs/engineering-process.md) and [decisions.md](docs/decisions.md) for implementation notes.

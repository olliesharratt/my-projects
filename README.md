# Software Engineering Portfolio

Projects exploring application development, data processing, algorithms, and systems programming using C#/.NET, C, Angular, and JavaScript.

## Featured Projects

### Reporting Automation Tool
A .NET command-line tool that validates weekly CSV reports, imports valid rows into SQLite, and writes an error report for invalid rows. Imports are transactional and idempotent.

**Stack:** C# / .NET 8, CsvHelper, SQLite, xUnit

[Overview, sample output, and run instructions](project3/ra/README.md)

### CS Fundamentals
An LRU key-value store, a BM25 search engine, and an interactive pathfinding visualiser, with design notes and runnable examples.

[Project overview](project4/csf/README.md) · [Open the pathfinding visualiser source](project4/csf/pathfinding-visualiser/index.html)

### Systems Projects
A POSIX mini-shell, a bounded pthread pool, and a small HTTP/1.1 server built on raw sockets.

[Project overview and test commands](project5/sys/README.md)

## In Progress

- [HS&E Incident and Near-Miss Reporting](project2/hse/README.md): runnable Angular frontend and ASP.NET Core API; persistence and automated tests are not implemented yet.
- [Asset Monitoring Dashboard](project1/README.md): project scaffold only. The source folders referenced by its solution and Docker Compose configuration are not present in this checkout, so it is not currently runnable.

## Technologies

C#, .NET, Angular, TypeScript, C, POSIX, JavaScript, PostgreSQL, SQLite, Docker, xUnit, and GitHub Actions.

# Decisions
1. **SQLite:** zero setup for a reporting tool; the `ImportService` SQL is portable to SQL Server/PostgreSQL.
2. **Raw string rows then validate:** one bad cell never crashes the parser, and every problem is reported with a line number.
3. **Content hash for idempotency:** renaming a file doesn't fool it, and re-running a scheduled job is safe.
4. **Valid rows load, invalid rows are reported:** exit code 2 signals a dirty file to a scheduler.

# Log events

Each log line the app writes carries an event ID, such as `[203]`. This page lists every event: what it means and
what to do about it.

IDs are grouped by area:

| Range | Area |
|---|---|
| 1xx | Database check at startup |
| 2xx | Folder run |
| 3xx | One file (the ingestion service) |
| 4xx | EDI reader |

## Events

| ID | Name | Level | Meaning | What to do |
|---|---|---|---|---|
| 101 | CannotConnect | Error | The app can't open the database. | Start SQL Server, check `ConnectionStrings:EdiDb`, then create the database (see the README, *3. The database*). |
| 102 | MigrationsMissing | Error | The database exists, but its migrations aren't applied. | Run `dotnet dotnet-ef database update --project src/EdiConsole` from `vicente-m/`. |
| 201 | RunStarted | Information | A run began: the folder and the SNIP level. | None. |
| 202 | FileDone | Information | A file finished: its outcome and counts. | If Rejected, read its rows in `ValidationIssues`. |
| 203 | FileFailed | Error | A file couldn't be processed. The exception is attached. | Read the exception. Nothing from that file was saved, so fix the cause and run again. |
| 204 | RunFinished | Information | The totals for the run. | None. |
| 301 | FileRead | Debug | The file's size and SHA-256. | None. |
| 302 | DuplicateSkipped | Information | The same bytes were ingested before, so nothing is stored. | None. |
| 303 | TransactionNotProcessed | Debug | A transaction isn't an 837P, or can't be read. It's stored as an issue. | Read its code in `ValidationIssues`. |
| 304 | TransactionInvalid | Debug | EdiFabric found errors at the configured SNIP level. They're stored as issues. | Read the issues in `ValidationIssues`. |
| 305 | TransactionMapped | Debug | A valid transaction became claims. | None. |
| 306 | TransactionUnmappable | Warning | A transaction is valid, but a required value is missing, so it can't be mapped. It's stored as an issue. | Read its issue in `ValidationIssues`. The file lacks data our model needs. |
| 307 | FileDecided | Debug | The file is Accepted or Rejected, with its counts. | None. |
| 308 | FileSaved | Debug | The `IngestedFiles` row was written. | Look it up by its `Id`. |
| 401 | ItemRead | Debug | The EDI reader yielded an item: ISA, GS, a transaction, GE or IEA. | None. |
| 402 | UnexpectedItem | Warning | The reader yielded something the parser doesn't handle. It's stored as an issue. | Report it: the reader or the file type may be new. |

## How the tests keep the codes honest

The table and the code must agree. If they drift, a filter on an ID means the wrong thing. The code declares each
event once, and a test compares the code with this table on every test run.

```
   src/**/*Log.cs                       docs/log-events.md
   [LoggerMessage] attributes           the "## Events" table
   EventId · EventName · Level          ID · Name · Level
             │                                   │
             └──────────────┬────────────────────┘
                            ▼
                 LogEventCatalogueTests
                            │
               fails when any of these differ
```

### What the test checks

- No two events share an ID, and no two share a name.
- Every event in the code has a row in the table, and every row has an event in the code.
- Each row's ID, name and level match the code.

The test reads the attributes by reflection and the table as text. It parses no EDI, so it spends no EdiFabric
operations.

### What it doesn't check

- **The meaning and "What to do" text.** Keep those accurate yourself.
- **Where an event is logged.** The test doesn't know which line calls it.
- **History.** The test sees only today's code, so it can't tell that an ID was used before. Don't reuse an ID that
  was ever shipped: renumbering breaks any filter or alert that uses the old number. To remove an event, delete its
  attribute and its row, and leave its ID unused.

### Adding an event

1. Choose the next free ID in the area, for example 403 for the next EDI reader event.
2. Add the `[LoggerMessage]` with a new, unique `EventName`.
3. Add its row to the table, in the same change.
4. Run the test from `vicente-m/`:

   ```powershell
   dotnet test EdiConsole.sln --filter FullyQualifiedName~LogEventCatalogueTests
   ```

### When the test fails

The failure message lists the rows that differ. Then decide which side is wrong:

- **A new event with no row:** add the row.
- **A row with no event:** delete the row, or restore the event if it was removed by mistake.
- **The same ID twice:** give the new event the next free ID in its area.
- **A name or level that differs:** correct the row to match the code, unless the code change was a mistake.

The meanings and actions are written by people. The test keeps the factual columns (ID, name and level) in step with
the code, because those are the columns that break silently.

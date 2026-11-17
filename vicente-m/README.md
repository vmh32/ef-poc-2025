# EdiConsole

Ingests HIPAA 837P files with EdiFabric into SQL Server Express. Each run reads every file in a folder, validates it,
and saves it with its claims (accepted) or with its validation issues (rejected).

## Before the first run

### 1. The .NET 10 SDK

The app targets .NET 10 (`net10.0`). `global.json` asks for SDK 10.0.100 or any later 10.0.x, so you need a .NET 10 SDK
installed. Check it from `vicente-m/`:

```powershell
dotnet --version   # must print 10.0.x; global.json picks it even if newer SDKs are installed
```

### 2. EdiFabric serial key

It is never committed; store it in User Secrets once:

```powershell
cd src/EdiConsole
dotnet user-secrets set "EdiFabric:SerialKey" "<your key>"
```

On a machine without User Secrets (e.g. CI), set the `EdiFabric__SerialKey` environment variable instead.

### 3. The database

You need SQL Server Express running locally as `.\SQLEXPRESS`, with Windows authentication. The connection string is
`ConnectionStrings:EdiDb` in `appsettings.Development.json`; override it with the `ConnectionStrings__EdiDb`
environment variable for another server.

Create the database (`EdiIngestion`) from the migrations, from `vicente-m/`:

```powershell
dotnet tool restore                                         # installs dotnet-ef 10.0.15, pinned in .config/dotnet-tools.json
dotnet dotnet-ef database update --project src/EdiConsole   # creates the database, or applies new migrations
```

If the database is missing or out of date, the app stops at startup and prints this command.

## Running the app

The app processes the `*.edi` files of one folder, set by `FileSource:Folder`.
The default (`appsettings.Development.json`) is `samples`, which works when you run from `vicente-m/`:

```powershell
cd vicente-m
dotnet run --project src/EdiConsole
```

`dotnet run` needs a project, not a solution, so from `vicente-m/` pass `--project src/EdiConsole`.

### What a run does

Each file ends with one outcome:

| Outcome | When | What is saved |
|---|---|---|
| `Accepted` | every transaction in the file is a valid 837P | the file and its claims |
| `Rejected` | anything is wrong: a validation error, another transaction type, unreadable or empty | the file and its issues, no claims |
| `Duplicate` | the same bytes were ingested before (accepted or rejected) | nothing |

All or nothing: one problem anywhere in a file rejects the whole file. A second run over the same folder reports every
file as `Duplicate`.

### Using another folder

A relative path is resolved against the folder you run the command from; an absolute path works anywhere.

| How | Example | Lasts |
|---|---|---|
| Command-line argument (after `--`) | `dotnet run --project src/EdiConsole -- --FileSource:Folder=C:\edi\inbox` | this run |
| Environment variable, PowerShell | `$env:FileSource__Folder = "C:\edi\inbox"` | this terminal session |
| Environment variable, bash | `export FileSource__Folder=/c/edi/inbox` | this terminal session |
| `appsettings.Development.json` | `"FileSource": { "Folder": "C:\\edi\\inbox" }` | until changed |

If several are set, the command line wins over the environment variable, which wins over `appsettings`.

### Running from the project folder

From `src/EdiConsole`, plain `dotnet run` works, but `samples` would point to `src/EdiConsole/samples`.
Point it at the samples two levels up:

```powershell
cd src/EdiConsole
dotnet run -- --FileSource:Folder=..\..\samples
```

If the folder doesn't exist, the app stops and prints the full path it tried.

### Other settings

- `FileSource:SearchPattern` (default `*.edi`) selects which files are read. Subfolders are not searched.
- `Ingestion:SnipLevel` (default `4`, in `appsettings.json`) is the SNIP level EdiFabric validates at, 1–4.
  Example: `dotnet run --project src/EdiConsole -- --Ingestion:SnipLevel=1`.

## Looking at the results

With `sqlcmd` (`-E` Windows sign-in, `-C` trust the local server's self-signed certificate, `-d` database,
`-W` trim the padding):

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -W -d EdiIngestion -Q "SELECT Id, FileName, Status, ClaimCount, ErrorCount FROM IngestedFiles"
sqlcmd -S .\SQLEXPRESS -E -C -W -d EdiIngestion -Q "SELECT IngestedFileId, Segment, SegmentPosition, Code, Message FROM ValidationIssues"
sqlcmd -S .\SQLEXPRESS -E -C -W -d EdiIngestion -Q "SELECT IngestedFileId, PatientControlNumber, TotalCharge, Patient_Name_LastOrOrganizationName FROM Claims"
```

Or connect a GUI (the SQL Server extension for VS Code, or SSMS) to `.\SQLEXPRESS` with Windows authentication and
"trust server certificate".

To ingest the same files again, start from an empty database:

```powershell
dotnet dotnet-ef database drop --project src/EdiConsole --force
dotnet dotnet-ef database update --project src/EdiConsole
```

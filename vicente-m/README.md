# EdiConsole

Ingests HIPAA 837P files with EdiFabric. It currently reads every file in a folder, parses it and maps the claims.

## Before the first run

The app needs an EdiFabric serial key. It is never committed; store it in User Secrets once:

```powershell
cd src/EdiConsole
dotnet user-secrets set "EdiFabric:SerialKey" "<your key>"
```

On a machine without User Secrets (e.g. CI), set the `EdiFabric__SerialKey` environment variable instead.

## Running the app

The app processes the `*.edi` files of one folder, set by `FileSource:Folder`.
The default (`appsettings.Development.json`) is `samples`, which works when you run from `vicente-m/`:

```powershell
cd vicente-m
dotnet run --project src/EdiConsole
```

`dotnet run` needs a project, not a solution, so from `vicente-m/` pass `--project src/EdiConsole`.

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
  Only transactions with no issues are mapped. Example: `dotnet run --project src/EdiConsole -- --Ingestion:SnipLevel=1`.
- Each run parses every file and spends EdiFabric operations (the community plan allows 250 a day).

using EdiConsole.Configuration;
using EdiConsole.Ingestion;
using EdiConsole.Mapping;
using EdiConsole.Parsing;
using EdiConsole.Persistence;
using EdiConsole.Sourcing;
using EdiConsole.Validation;
using EdiFabric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// The content root is where appsettings*.json are read from. By default it's the current directory, so running
// from another folder (e.g. vicente-m/) would silently skip them. The build copies them to the output folder,
// so reading them from there works wherever the app is started.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// ── Configuration, validated at startup ─────────────────────────────────────────────────────────────────────

builder.Services.AddOptions<EdiFabricOptions>()
    .BindConfiguration(EdiFabricOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.SerialKey),
        "EdiFabric:SerialKey is missing. For local development run: dotnet user-secrets set \"EdiFabric:SerialKey\" \"<your key>\"");

builder.Services.AddOptions<FileSourceOptions>()
    .BindConfiguration(FileSourceOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Folder),
        "FileSource:Folder is missing. Set it in appsettings.<Environment>.json or with the FileSource__Folder environment variable.");

builder.Services.AddOptions<IngestionOptions>()
    .BindConfiguration(IngestionOptions.SectionName)
    .Validate(
        options => options.SnipLevel is >= 1 and <= 4,
        "Ingestion:SnipLevel must be 1–4 (EdiFabric's SNIP levels).");

var connectionString = builder.Configuration.GetConnectionString(EdiDbContext.ConnectionStringName)
    ?? throw new InvalidOperationException(
        $"ConnectionStrings:{EdiDbContext.ConnectionStringName} is missing. Set it in appsettings.<Environment>.json " +
        $"or with the ConnectionStrings__{EdiDbContext.ConnectionStringName} environment variable.");

// ── Services ────────────────────────────────────────────────────────────────────────────────────────────────

// Stateless: one instance for the whole run.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEdiFileSource, LocalFolderFileSource>();
builder.Services.AddSingleton<IEdiParser, EdiFabricParser>();
builder.Services.AddSingleton<ITransactionValidator, EdiFabricValidator>();
builder.Services.AddSingleton<IClaimMapper, ClaimMapper>();

// Scoped: one per file (see the loop below). The DbContext tracks what it saves, so it must not outlive a file.
builder.Services.AddDbContext<EdiDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IIngestionStore, EfIngestionStore>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

using var host = builder.Build();

// EdiFabric's license is process-wide (static), so set it once, before any EDI is read or written.
var ediFabricOptions = host.Services.GetRequiredService<IOptions<EdiFabricOptions>>().Value;
License.SetSerial(ediFabricOptions.SerialKey);

// ── The database must exist and be up to date before any file is read ──────────────────────────────────────

await using (var scope = host.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<EdiDbContext>().Database;
    const string howToFix = "From vicente-m/ run: dotnet dotnet-ef database update --project src/EdiConsole";

    if (!await database.CanConnectAsync())
    {
        Console.Error.WriteLine($"Can't connect to the database (ConnectionStrings:{EdiDbContext.ConnectionStringName}). " +
                                $"Is SQL Server running, and was the database created? {howToFix}");
        Environment.ExitCode = 1;
        return;
    }

    var pending = (await database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        Console.Error.WriteLine($"The database is missing {pending.Count} migration(s): {string.Join(", ", pending)}. {howToFix}");
        Environment.ExitCode = 1;
        return;
    }
}

// ── Ingest every file in the folder ─────────────────────────────────────────────────────────────────────────

var source = host.Services.GetRequiredService<IEdiFileSource>();
var folder = Path.GetFullPath(host.Services.GetRequiredService<IOptions<FileSourceOptions>>().Value.Folder);
var snipLevel = host.Services.GetRequiredService<IOptions<IngestionOptions>>().Value.SnipLevel;
Console.WriteLine($"Ingesting {folder} at SNIP level {snipLevel}");

var results = new List<IngestionResult>();
var failures = 0;

await foreach (var file in source.ListAsync())
{
    // One DI scope per file: the file gets its own DbContext, store and service, so one file's save
    // can never pick up another file's entities.
    await using var scope = host.Services.CreateAsyncScope();
    var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionService>();

    try
    {
        var result = await ingestion.IngestAsync(file);
        results.Add(result);

        var detail = result.Outcome switch
        {
            IngestionOutcome.Accepted => $"{result.ClaimCount} claim(s)",
            IngestionOutcome.Rejected => $"{result.ErrorCount} issue(s)",
            _ => "already ingested: skipped",
        };
        Console.WriteLine($"  {file.Name,-40} {result.Outcome,-10} {detail}");
    }
    catch (Exception ex)                                    // one file failing (e.g. I/O, database) mustn't stop the others
    {
        failures++;
        Console.WriteLine($"  {file.Name,-40} {"FAILED",-10} {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine(
    $"Done: {results.Count(r => r.Outcome == IngestionOutcome.Accepted)} accepted, " +
    $"{results.Count(r => r.Outcome == IngestionOutcome.Rejected)} rejected, " +
    $"{results.Count(r => r.Outcome == IngestionOutcome.Duplicate)} duplicate(s), {failures} failed.");

if (failures > 0)
    Environment.ExitCode = 1;

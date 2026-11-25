using EdiConsole.Configuration;
using EdiConsole.Ingestion;
using EdiConsole.Persistence;
using EdiConsole.Sourcing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// The content root is where appsettings*.json are read from. By default it's the current directory, so running
// from another folder (e.g. vicente-m/) would silently skip them. The build copies them to the output folder,
// so reading them from there works wherever the app is started.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddEdiFabric();
builder.Services.AddIngestion(builder.Configuration);

using var host = builder.Build();

host.Services.ApplyEdiFabricLicense();   // process-wide, once, before any EDI is read or written

// The database must exist and be up to date before any file is read.
if (!await host.Services.GetRequiredService<DatabaseCheck>().PassesAsync(Console.Out))
{
    Environment.ExitCode = 1;
    return;
}

// The file source must be ready too: the folder must exist, or the bucket must answer.
if (!await host.Services.GetRequiredService<IFileSourceCheck>().PassesAsync(Console.Out))
{
    Environment.ExitCode = 1;
    return;
}

var summary = await host.Services.GetRequiredService<FolderIngestion>().RunAsync(Console.Out);

if (summary.Failed > 0 || summary.ListingFailed)
    Environment.ExitCode = 1;

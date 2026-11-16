using EdiConsole.Configuration;
using EdiConsole.Mapping;
using EdiConsole.Parsing;
using EdiConsole.Services;
using EdiConsole.Sourcing;
using EdiFabric;
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

// Stateless services: one instance for the whole run.
builder.Services.AddSingleton<IEdiFileSource, LocalFolderFileSource>();
builder.Services.AddSingleton<IEdiParser, EdiFabricParser>();
builder.Services.AddSingleton<IClaimMapper, ClaimMapper>();

builder.Services.AddSingleton<IGreeter, Greeter>();

using var host = builder.Build();

// EdiFabric's license is process-wide (static), so set it once, before any EDI is read or written.
var ediFabricOptions = host.Services.GetRequiredService<IOptions<EdiFabricOptions>>().Value;
License.SetSerial(ediFabricOptions.SerialKey);

// ── TEMPORARY PROBE: list the folder, parse every file and map the clean 837Ps. Replaced by the facade later.
var source = host.Services.GetRequiredService<IEdiFileSource>();
var parser = host.Services.GetRequiredService<IEdiParser>();
var mapper = host.Services.GetRequiredService<IClaimMapper>();

await foreach (var file in source.ListAsync())
{
    Console.WriteLine(file.Name);
    try
    {
        await using var stream = await source.OpenReadAsync(file);
        var transactions = 0;

        await foreach (var parsed in parser.ParseAsync(stream))
        {
            transactions++;
            var header = $"  ISA {parsed.InterchangeControlNumber} ({parsed.UsageIndicator}), ST {parsed.TransactionSetControlNumber}:";

            if (parsed.Problem is { } problem)
            {
                Console.WriteLine($"{header} {problem.Code}: {problem.Message}");
            }
            else if (parsed.Transaction!.HasErrors)
            {
                Console.WriteLine($"{header} 837P with parse errors, not mapped (the validator will report them)");
            }
            else
            {
                var claims = mapper.Map(parsed.Transaction);
                Console.WriteLine($"{header} 837P, {claims.Count} claim(s)");
                foreach (var claim in claims)
                {
                    Console.WriteLine(
                        $"    {claim.PatientControlNumber}: {claim.TotalCharge:0.00}, patient {claim.Patient.Name.FirstName} {claim.Patient.Name.LastOrOrganizationName}, " +
                        $"{claim.Diagnoses.Count} diagnosis(es), {claim.ServiceLines.Count} line(s)");
                }
            }
        }

        if (transactions == 0)
            Console.WriteLine("  empty: no transactions");
    }
    catch (Exception ex)                                    // one bad file mustn't stop the others
    {
        Console.WriteLine($"  FAILED: {ex.GetType().Name}: {ex.Message}");
    }
}

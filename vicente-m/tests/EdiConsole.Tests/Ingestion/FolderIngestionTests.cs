using System.Runtime.CompilerServices;
using EdiConsole.Ingestion;
using EdiConsole.Sourcing;
using EdiConsole.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EdiConsole.Tests.Ingestion;

/// <summary>
/// <see cref="FolderIngestion"/> when the list of files can't be read: the run stops, says so, and keeps the counts of
/// the files done before. A fake source and a fake facade, so no EDI is parsed and no database is used.
/// </summary>
public sealed class FolderIngestionTests
{
    private const string Description = "s3://edi-bucket/incoming/";

    [Fact]
    public async Task A_listing_that_fails_stops_the_run_and_keeps_the_counts_so_far()
    {
        var source = new FakeSource(["a.edi"], thenFails: new IOException("The connection was reset."));
        var logger = new RecordingLogger<FolderIngestion>();
        var output = new StringWriter();

        var summary = await CreateRun(source, logger).RunAsync(output);

        Assert.True(summary.ListingFailed);
        Assert.Equal(1, summary.Accepted);
        var report = output.ToString();
        Assert.Contains($"Listing {Description} failed: IOException: The connection was reset.", report);
        Assert.Contains("Stopped: 1 accepted", report);
        var entry = Assert.Single(logger.Entries, e => e.EventId.Name == "ListingFailed");
        Assert.IsType<IOException>(entry.Exception);
    }

    [Fact]
    public async Task A_listing_that_ends_normally_is_Done()
    {
        var logger = new RecordingLogger<FolderIngestion>();
        var output = new StringWriter();

        var summary = await CreateRun(new FakeSource([]), logger).RunAsync(output);

        Assert.False(summary.ListingFailed);
        Assert.Contains("Done: 0 accepted", output.ToString());
        Assert.DoesNotContain(logger.Entries, e => e.EventId.Name == "ListingFailed");
    }

    private static FolderIngestion CreateRun(IEdiFileSource source, RecordingLogger<FolderIngestion> logger)
    {
        var services = new ServiceCollection();
        services.AddScoped<IIngestionService, AcceptEverything>();
        return new FolderIngestion(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            source,
            Options.Create(new IngestionOptions()),
            logger);
    }

    /// <summary>Lists the given names. With <paramref name="thenFails"/>, throws it after the last name instead of ending.</summary>
    private sealed class FakeSource(string[] names, Exception? thenFails = null) : IEdiFileSource
    {
        public string Description => FolderIngestionTests.Description;

        public async IAsyncEnumerable<EdiFile> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var name in names)
            {
                await Task.Yield();
                yield return new EdiFile(name, Description + name);
            }

            if (thenFails is not null)
                throw thenFails;
        }

        public Task<Stream> OpenReadAsync(EdiFile file, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The fake facade never reads a file.");
    }

    /// <summary>A facade that accepts every file with one claim, without reading it.</summary>
    private sealed class AcceptEverything : IIngestionService
    {
        public Task<IngestionResult> IngestAsync(EdiFile file, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IngestionResult(file.Name, IngestionOutcome.Accepted, ClaimCount: 1, ErrorCount: 0));
    }
}

using System.Diagnostics;
using EdiConsole.Sourcing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdiConsole.Ingestion;

/// <summary>
/// Ingests every file the source lists, one at a time, in name order. Prints one line per file and a summary.
/// </summary>
/// <remarks>
/// <para>
/// Each file gets its own DI scope, and so its own facade, store and DbContext. A file that fails (I/O, database)
/// is counted and reported, and the run goes on with the next file. If the list of files itself can't be read (a
/// deleted bucket, a dropped connection), the run stops: that's reported too, and the summary says so.
/// </para>
/// <para>
/// Two outputs: the run report goes to <c>output</c> (the console's stdout), and the logs go to the
/// <see cref="ILogger"/> (stderr, see appsettings.json). Logs carry codes, counts and file names, never claim data.
/// </para>
/// </remarks>
public sealed class FolderIngestion(
    IServiceScopeFactory scopeFactory,
    IEdiFileSource source,
    IOptions<IngestionOptions> ingestionOptions,
    ILogger<FolderIngestion> logger)
{
    public async Task<FolderIngestionSummary> RunAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        var description = source.Description;
        var snipLevel = ingestionOptions.Value.SnipLevel;
        output.WriteLine($"Ingesting {description} at SNIP level {snipLevel}");
        logger.RunStarted(description, snipLevel);

        var results = new List<IngestionResult>();
        var failed = 0;
        var listingFailed = false;

        // Not await foreach: a failure while listing has to be told apart from a failure while ingesting a file.
        await using var files = source.ListAsync(cancellationToken).GetAsyncEnumerator();
        while (true)
        {
            EdiFile file;
            try
            {
                if (!await files.MoveNextAsync())
                    break;
                file = files.Current;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)   // the list can't be read: no file can follow
            {
                listingFailed = true;
                output.WriteLine($"Listing {description} failed: {ex.GetType().Name}: {ex.Message}");
                logger.ListingFailed(ex, description);
                break;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionService>();
            using var fileScope = logger.BeginFileScope(file.Name);    // every log line for this file names it
            var started = Stopwatch.GetTimestamp();

            try
            {
                var result = await ingestion.IngestAsync(file, cancellationToken);
                results.Add(result);

                var detail = result.Outcome switch
                {
                    IngestionOutcome.Accepted => $"{result.ClaimCount} claim(s)",
                    IngestionOutcome.Rejected => $"{result.ErrorCount} issue(s)",
                    _ => "already ingested: skipped",
                };
                output.WriteLine($"  {file.Name,-40} {result.Outcome,-10} {detail}");
                logger.FileDone(file.Name, result.Outcome, result.ClaimCount, result.ErrorCount, ElapsedMs(started));
            }
            catch (Exception ex)                                // one file failing (e.g. I/O, database) mustn't stop the others
            {
                failed++;
                output.WriteLine($"  {file.Name,-40} {"FAILED",-10} {ex.GetType().Name}: {ex.Message}");

                // The exception object, not just its message: the log keeps the stack trace.
                logger.FileFailed(ex, file.Name, ElapsedMs(started));
            }
        }

        var summary = new FolderIngestionSummary(
            Accepted: results.Count(r => r.Outcome == IngestionOutcome.Accepted),
            Rejected: results.Count(r => r.Outcome == IngestionOutcome.Rejected),
            Duplicates: results.Count(r => r.Outcome == IngestionOutcome.Duplicate),
            Failed: failed,
            ListingFailed: listingFailed);

        output.WriteLine(
            $"{(listingFailed ? "Stopped" : "Done")}: {summary.Accepted} accepted, {summary.Rejected} rejected, " +
            $"{summary.Duplicates} duplicate(s), {summary.Failed} failed.");
        logger.RunFinished(summary.Accepted, summary.Rejected, summary.Duplicates, summary.Failed);

        return summary;
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}

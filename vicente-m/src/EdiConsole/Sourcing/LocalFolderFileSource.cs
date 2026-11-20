using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace EdiConsole.Sourcing;

/// <summary>
/// <see cref="IEdiFileSource"/> on a local folder (Phase 1). Phase 2 adds an S3 source behind the same interface.
/// </summary>
public sealed class LocalFolderFileSource(IOptions<FileSourceOptions> options) : IEdiFileSource
{
    private readonly FileSourceOptions _options = options.Value;

    /// <summary>The folder's full path, as the run report shows it. Doesn't check that the folder exists.</summary>
    public string Description => Path.GetFullPath(_options.Folder);   // relative → against the current directory

    /// <summary>
    /// Lists the matching files, sorted by name so every run processes them in the same order.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The folder doesn't exist; the message shows the full path tried.</exception>
    public async IAsyncEnumerable<EdiFile> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folder = Path.GetFullPath(_options.Folder);             // relative → against the current directory
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException(
                $"FileSource:Folder \"{_options.Folder}\" resolves to \"{folder}\", which doesn't exist. " +
                "Run from the folder it's relative to, or set it with the FileSource__Folder environment variable.");
        }

        var paths = Directory.EnumerateFiles(folder, _options.SearchPattern, SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new EdiFile(Path.GetFileName(path), path);   // SourceUri = the full path
        }

        // Listing a local folder is synchronous; the interface is asynchronous because S3 (Phase 2) isn't.
        // This line only keeps the compiler from warning that an async method has no await (CS1998).
        await Task.CompletedTask;
    }

    /// <summary>Opens the file for reading. The caller disposes the stream.</summary>
    public Task<Stream> OpenReadAsync(EdiFile file, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Stream stream = new FileStream(file.SourceUri, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,                                 // others may read it at the same time, not change it
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,   // read once, start to end, without blocking
        });

        return Task.FromResult(stream);
    }
}

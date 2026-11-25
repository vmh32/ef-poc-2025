using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdiConsole.Sourcing;

/// <summary>
/// <see cref="IFileSourceCheck"/> for the local folder: the folder must exist.
/// </summary>
public sealed class LocalFolderCheck(IOptions<FileSourceOptions> options, ILogger<LocalFolderCheck> logger) : IFileSourceCheck
{
    /// <summary>
    /// Prints the folder it's using to <paramref name="output"/>, then checks it. The line is printed either way, so
    /// the run report names the folder even when the check fails and the run never starts.
    /// </summary>
    public Task<bool> PassesAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        var setting = options.Value.Folder;
        var folder = Path.GetFullPath(setting);                     // relative → against the current directory
        output.WriteLine($"Using folder {folder}");

        if (Directory.Exists(folder))
        {
            return Task.FromResult(true);
        }

        logger.FolderNotFound(folder, setting);
        return Task.FromResult(false);
    }
}

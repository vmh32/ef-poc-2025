using EdiConsole.Sourcing;

namespace EdiConsole.Tests.Support;

internal static class SourceListings
{
    /// <summary>Collects a source's whole listing, for tests that check what it returns.</summary>
    public static async Task<List<EdiFile>> ListFilesAsync(this IEdiFileSource source)
    {
        var files = new List<EdiFile>();
        await foreach (var file in source.ListAsync())
        {
            files.Add(file);
        }

        return files;
    }
}

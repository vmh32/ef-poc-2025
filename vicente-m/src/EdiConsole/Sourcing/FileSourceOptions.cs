namespace EdiConsole.Sourcing;

/// <summary>
/// Settings for <see cref="LocalFolderFileSource"/>, bound from the "FileSource" configuration section
/// (appsettings, or environment variables such as <c>FileSource__Folder</c>).
/// </summary>
public sealed class FileSourceOptions
{
    public const string SectionName = "FileSource";

    /// <summary>
    /// The folder to ingest. A relative path is resolved against the current directory,
    /// i.e. where the app is started from (e.g. "samples" when run from vicente-m/).
    /// </summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>Which files to pick up. Only the folder itself is searched, not subfolders.</summary>
    public string SearchPattern { get; set; } = "*.edi";
}

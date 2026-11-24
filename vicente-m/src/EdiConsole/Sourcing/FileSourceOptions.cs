namespace EdiConsole.Sourcing;

/// <summary>
/// Settings for the file source, bound from the "FileSource" configuration section
/// (appsettings, or environment variables such as <c>FileSource__Kind</c>).
/// </summary>
public sealed class FileSourceOptions
{
    public const string SectionName = "FileSource";

    /// <summary>Where this run reads from: a local folder (<see cref="Folder"/>) or an S3 bucket (<see cref="S3SourceOptions"/>).</summary>
    public FileSourceKind Kind { get; set; } = FileSourceKind.Local;

    /// <summary>
    /// The folder to ingest when <see cref="Kind"/> is Local. A relative path is resolved against the current directory,
    /// i.e. where the app is started from (e.g. "samples" when run from vicente-m/).
    /// </summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>Which file names to pick up, e.g. *.edi. Subfolders are skipped.</summary>
    public string SearchPattern { get; set; } = "*.edi";
}

namespace EdiConsole.Sourcing;

/// <summary>
/// Where this run reads its files from, set by <c>FileSource:Kind</c>.
/// </summary>
public enum FileSourceKind
{
    /// <summary>A folder on this machine, set by <c>FileSource:Folder</c>.</summary>
    Local,

    /// <summary>An S3 bucket, set by the <c>S3Source</c> section.</summary>
    S3,
}

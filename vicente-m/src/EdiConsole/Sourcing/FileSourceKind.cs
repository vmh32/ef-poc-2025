namespace EdiConsole.Sourcing;

/// <summary>
/// Where this run reads its files from, set by <c>FileSource:Kind</c>.
/// Only the local folder exists so far; the S3 source is added as its own step.
/// </summary>
public enum FileSourceKind
{
    /// <summary>A folder on this machine, set by <c>FileSource:Folder</c>.</summary>
    Local,
}

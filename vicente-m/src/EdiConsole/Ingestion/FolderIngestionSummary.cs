namespace EdiConsole.Ingestion;

/// <summary>The counts for one run over a folder, for the console and the exit code.</summary>
/// <param name="ListingFailed">The list of files couldn't be read, so the run stopped early.</param>
public sealed record FolderIngestionSummary(int Accepted, int Rejected, int Duplicates, int Failed, bool ListingFailed);

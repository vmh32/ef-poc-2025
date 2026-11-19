namespace EdiConsole.Ingestion;

/// <summary>The counts for one run over a folder, for the console and the exit code.</summary>
public sealed record FolderIngestionSummary(int Accepted, int Rejected, int Duplicates, int Failed);

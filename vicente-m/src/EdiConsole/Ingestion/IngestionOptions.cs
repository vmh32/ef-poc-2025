namespace EdiConsole.Ingestion;

/// <summary>
/// Settings for the ingestion workflow, bound from the "Ingestion" configuration section
/// (appsettings, or environment variables such as <c>Ingestion__SnipLevel</c>).
/// </summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>
    /// The SNIP level EdiFabric validates at: 1–4 (4 = the strictest EdiFabric offers). Our good samples pass level 4.
    /// </summary>
    public byte SnipLevel { get; set; } = 4;
}

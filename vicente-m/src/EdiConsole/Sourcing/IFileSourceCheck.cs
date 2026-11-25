namespace EdiConsole.Sourcing;

/// <summary>
/// Startup check for the file source that FileSource:Kind selects, run before any file is listed: the folder must exist,
/// or the bucket must answer. Like the database check, it logs why the source isn't ready.
/// </summary>
public interface IFileSourceCheck
{
    /// <summary>Checks the source, and logs why it isn't ready if it isn't.</summary>
    /// <param name="output">The run report (stdout). The check may print what it uses.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>True if the app can go on; false if it must stop.</returns>
    Task<bool> PassesAsync(TextWriter output, CancellationToken cancellationToken = default);
}

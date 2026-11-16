namespace EdiConsole.Parsing;

/// <summary>
/// Describes why a transaction set couldn't be returned as an 837P. The facade stores it as a
/// <see cref="Domain.Tracking.ValidationIssue"/>, copying <see cref="Code"/> and <see cref="Message"/> as they are.
/// </summary>
/// <param name="Code">A stable code to store and query, one of the constants below. Never reworded.</param>
/// <param name="Message">A human-readable explanation. Free to be reworded.</param>
public sealed record ParseProblem(string Code, string Message)
{
    /// <summary>A transaction set that isn't an 837P, e.g. an 837I.</summary>
    public const string UnsupportedTransaction = "UnsupportedTransaction";

    /// <summary>Content EdiFabric's reader couldn't read, e.g. a 275 (no template) or a broken envelope.</summary>
    public const string Unreadable = "Unreadable";

    /// <summary>An item from the reader that the parser doesn't know how to handle.</summary>
    public const string UnexpectedItem = "UnexpectedItem";
}

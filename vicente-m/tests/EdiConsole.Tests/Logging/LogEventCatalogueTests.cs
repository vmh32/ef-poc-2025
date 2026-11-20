using System.Reflection;
using System.Text.RegularExpressions;
using EdiConsole.Ingestion;
using Microsoft.Extensions.Logging;

namespace EdiConsole.Tests.Logging;

/// <summary>
/// Keeps the event table in <c>docs/log-events.md</c> in step with the <c>[LoggerMessage]</c> events in the code.
/// Reads the attributes by reflection and the table as text: no EDI is parsed, so no EdiFabric operation is spent.
/// </summary>
public sealed class LogEventCatalogueTests
{
    private const string TableHeading = "## Events";

    // The first three cells of a table row: ID, name, level. The header and separator rows don't match.
    private static readonly Regex TableRow = new(@"^\|\s*(\d+)\s*\|\s*(\w+)\s*\|\s*(\w+)\s*\|", RegexOptions.Compiled);

    [Fact]
    public void Event_ids_are_unique()
    {
        var duplicates = CodeEvents().GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Event_names_are_unique()
    {
        var duplicates = CodeEvents().GroupBy(e => e.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Table_lists_every_event_and_nothing_else()
    {
        var inCode = CodeEvents().OrderBy(e => e.Id).ToList();
        var inTable = TableEvents().OrderBy(e => e.Id).ToList();

        Assert.Equal(inCode, inTable);
    }

    // Every [LoggerMessage] in the EdiConsole assembly. The log classes are internal, so start from a public type.
    private static IEnumerable<LogEvent> CodeEvents() =>
        typeof(IngestionService).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<LoggerMessageAttribute>())
            .OfType<LoggerMessageAttribute>()
            .Select(attribute => new LogEvent(attribute.EventId, attribute.EventName ?? "", attribute.Level.ToString()));

    // The rows under "## Events" only, so an example table in another section is never mistaken for an event.
    private static IEnumerable<LogEvent> TableEvents()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "log-events.md");
        var lines = File.ReadLines(path)
            .SkipWhile(line => line != TableHeading)
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal));

        foreach (var line in lines)
        {
            var match = TableRow.Match(line);
            if (match.Success)
                yield return new LogEvent(int.Parse(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value);
        }
    }

    private sealed record LogEvent(int Id, string Name, string Level);
}

using Microsoft.Extensions.Logging;

namespace EdiConsole.Tests.Support;

/// <summary>An <see cref="ILogger{T}"/> that keeps every entry, so a test can check which event was logged.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
}

/// <summary>One log call: its level, event, formatted message and exception.</summary>
internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

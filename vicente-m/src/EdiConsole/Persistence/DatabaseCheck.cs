using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdiConsole.Persistence;

/// <summary>
/// Startup check: the database must exist, and have every migration applied, before any file is read.
/// </summary>
/// <remarks>
/// A class in DI (not static) so it can receive its logger. It opens its own scope because the DbContext is scoped.
/// </remarks>
public sealed class DatabaseCheck(IServiceScopeFactory scopeFactory, ILogger<DatabaseCheck> logger)
{
    private const string HowToFix = "From vicente-m/ run: dotnet dotnet-ef database update --project src/EdiConsole";

    /// <summary>Prints the database it's using to <paramref name="output"/>, and logs why it isn't ready if it isn't.</summary>
    /// <returns>True if the app can go on; false if it must stop.</returns>
    public async Task<bool> PassesAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EdiDbContext>().Database;

        // Only the server and database names: the full connection string can hold a password.
        var connection = database.GetDbConnection();
        output.WriteLine($"Using database {connection.Database} on {connection.DataSource}");

        if (!await database.CanConnectAsync(cancellationToken))
        {
            logger.LogError(
                "Can't connect to database {Database} on {Server} (ConnectionStrings:{ConnectionStringName}). " +
                "Is SQL Server running, and was the database created? {HowToFix}",
                connection.Database, connection.DataSource, EdiDbContext.ConnectionStringName, HowToFix);
            return false;
        }

        var pending = (await database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogError(
                "Database {Database} is missing {PendingCount} migration(s): {PendingMigrations}. {HowToFix}",
                connection.Database, pending.Count, string.Join(", ", pending), HowToFix);
            return false;
        }

        return true;
    }
}

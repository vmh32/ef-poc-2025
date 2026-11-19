using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EdiConsole.Persistence;

/// <summary>
/// Startup check: the database must exist, and have every migration applied, before any file is read.
/// </summary>
public static class DatabaseCheck
{
    private const string HowToFix = "From vicente-m/ run: dotnet dotnet-ef database update --project src/EdiConsole";

    /// <summary>Prints the database it's using, and prints why it isn't ready if it isn't.</summary>
    /// <returns>True if the app can go on; false if it must stop.</returns>
    public static async Task<bool> PassesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EdiDbContext>().Database;

        // Only the server and database names: the full connection string can hold a password.
        var connection = database.GetDbConnection();
        Console.WriteLine($"Using database {connection.Database} on {connection.DataSource}");

        if (!await database.CanConnectAsync(cancellationToken))
        {
            Console.Error.WriteLine($"Can't connect to the database (ConnectionStrings:{EdiDbContext.ConnectionStringName}). " +
                                    $"Is SQL Server running, and was the database created? {HowToFix}");
            return false;
        }

        var pending = (await database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            Console.Error.WriteLine($"The database is missing {pending.Count} migration(s): {string.Join(", ", pending)}. {HowToFix}");
            return false;
        }

        return true;
    }
}

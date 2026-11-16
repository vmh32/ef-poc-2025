using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EdiConsole.Persistence;

/// <summary>
/// Creates the <see cref="EdiDbContext"/> for the <c>dotnet ef</c> tools (migrations, database update),
/// so they don't have to run Program.cs. Reads the salme configuration as the app:
/// appsettings files, then environment variables (e.g. <c>ConnectionStrings__EdiDb</c>).
/// </summary>
internal sealed class DesignTimeEdiDbContextFactory : IDesignTimeDbContextFactory<EdiDbContext>
{
    public EdiDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)                  // the appsettings files are copied to the output
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(EdiDbContext.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{EdiDbContext.ConnectionStringName} is missing (appsettings.{environment}.json or ConnectionStrings__{EdiDbContext.ConnectionStringName}).");

        var options = new DbContextOptionsBuilder<EdiDbContext>().UseSqlServer(connectionString).Options;
        return new EdiDbContext(options);
    }
}

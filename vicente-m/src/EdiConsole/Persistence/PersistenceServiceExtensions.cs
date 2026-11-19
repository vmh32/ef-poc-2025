using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EdiConsole.Persistence;

public static class PersistenceServiceExtensions
{
    /// <summary>
    /// Registers the EF Core context and the ingestion store on SQL Server. Both are scoped: the DbContext tracks
    /// what it saves, so it must not outlive one file.
    /// </summary>
    public static IServiceCollection AddEdiPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(EdiDbContext.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{EdiDbContext.ConnectionStringName} is missing. Set it in appsettings.<Environment>.json " +
                $"or with the ConnectionStrings__{EdiDbContext.ConnectionStringName} environment variable.");

        services.AddDbContext<EdiDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IIngestionStore, EfIngestionStore>();

        return services;
    }
}

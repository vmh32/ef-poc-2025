using EdiConsole.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace EdiConsole.Persistence;

/// <summary>
/// The EF Core session for ingestion results.
/// </summary>
/// <remarks>
/// Only the aggregate root, <see cref="IngestedFile"/>, has a <see cref="DbSet{TEntity}"/>: claims and issues
/// are always added through their file, and saved with it in one <c>SaveChanges</c>.
/// The mapping of each entity lives in its own class under <c>Persistence/Configurations</c>.
/// </remarks>
public sealed class EdiDbContext(DbContextOptions<EdiDbContext> options) : DbContext(options)
{
    /// <summary>The name of the connection string in configuration (ConnectionStrings:EdiDb).</summary>
    public const string ConnectionStringName = "EdiDb";

    public DbSet<IngestedFile> IngestedFiles => Set<IngestedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(EdiDbContext).Assembly);
}

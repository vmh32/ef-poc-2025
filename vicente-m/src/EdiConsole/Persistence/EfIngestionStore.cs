using EdiConsole.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace EdiConsole.Persistence;

/// <summary>
/// <see cref="IIngestionStore"/> on EF Core and SQL Server.
/// </summary>
public sealed class EfIngestionStore(EdiDbContext dbContext) : IIngestionStore
{
    public Task<bool> IsAlreadyIngestedAsync(string sha256, CancellationToken cancellationToken = default)
        => dbContext.IngestedFiles.AnyAsync(f => f.Sha256 == sha256, cancellationToken);

    public async Task SaveAsync(IngestedFile file, CancellationToken cancellationToken = default)
    {
        // Adding the root adds everything reachable from it: claims, their diagnoses and lines, and issues.
        // One SaveChanges runs in one database transaction, so the file is saved completely or not at all.
        dbContext.IngestedFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

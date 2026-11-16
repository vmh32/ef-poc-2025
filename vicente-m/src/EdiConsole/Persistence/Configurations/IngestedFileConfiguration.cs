using EdiConsole.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EdiConsole.Persistence.Configurations;

/// <summary>The aggregate root: one row per file processed, owning its claims and issues.</summary>
internal sealed class IngestedFileConfiguration : IEntityTypeConfiguration<IngestedFile>
{
    public void Configure(EntityTypeBuilder<IngestedFile> builder)
    {
        builder.ToTable("IngestedFiles");

        builder.Property(f => f.FileName).HasMaxLength(260);

        // SHA-256 as 64 hex characters. Unique: the same content is never ingested twice.
        builder.Property(f => f.Sha256).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        builder.HasIndex(f => f.Sha256).IsUnique();

        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);   // stored as 'Accepted' / 'Rejected'
        builder.Property(f => f.EdiFabricVersion).HasMaxLength(20).IsUnicode(false);
        builder.Property(f => f.InterchangeControlNumber).HasMaxLength(9);          // ISA13: 9 digits
        builder.Property(f => f.UsageIndicator).HasMaxLength(1);                    // ISA15: T or P

        // Saving a file saves its claims and issues; deleting a file deletes them.
        builder.HasMany(f => f.Claims).WithOne().HasForeignKey(c => c.IngestedFileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(f => f.Issues).WithOne().HasForeignKey(i => i.IngestedFileId).OnDelete(DeleteBehavior.Cascade);
    }
}

using EdiConsole.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EdiConsole.Persistence.Configurations;

/// <summary>One row per HI position that was sent.</summary>
internal sealed class ClaimDiagnosisConfiguration : IEntityTypeConfiguration<ClaimDiagnosis>
{
    public void Configure(EntityTypeBuilder<ClaimDiagnosis> builder)
    {
        builder.ToTable("ClaimDiagnoses");

        builder.Property(d => d.Qualifier).HasMaxLength(3);                         // HInn-1: ABK, ABF, BK, BF
        builder.Property(d => d.Code).HasMaxLength(30);                             // HInn-2

        // A position appears once per claim, and service lines join on it (SV107 → Sequence).
        builder.HasIndex(d => new { d.ClaimId, d.Sequence }).IsUnique();
    }
}

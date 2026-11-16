using EdiConsole.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EdiConsole.Persistence.Configurations;

/// <summary>One row per 2400 loop.</summary>
internal sealed class ServiceLineConfiguration : IEntityTypeConfiguration<ServiceLine>
{
    public void Configure(EntityTypeBuilder<ServiceLine> builder)
    {
        builder.ToTable("ServiceLines");

        builder.Property(l => l.ProcedureCode).HasMaxLength(48);                    // SV101-2
        builder.Property(l => l.Modifier1).HasMaxLength(2);                         // SV101-3..6
        builder.Property(l => l.Modifier2).HasMaxLength(2);
        builder.Property(l => l.Modifier3).HasMaxLength(2);
        builder.Property(l => l.Modifier4).HasMaxLength(2);
        builder.Property(l => l.Charge).HasPrecision(18, 2);                        // SV102: precision 18 from LineItemChargeAmount_02 (R 1–18); 2 decimals for money
        builder.Property(l => l.UnitBasis).HasMaxLength(2);                         // SV103: UN, MJ
        builder.Property(l => l.Units).HasPrecision(15, 3);                         // SV104: precision 15 from ServiceUnitCount_04 (R 1–15); 3 decimals for fractional units
        builder.Property(l => l.PlaceOfServiceCode).HasMaxLength(2);                // SV105

        builder.HasIndex(l => new { l.ClaimId, l.LineNumber }).IsUnique();         // LX01 is unique per claim
    }
}

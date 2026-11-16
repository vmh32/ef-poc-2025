using EdiConsole.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EdiConsole.Persistence.Configurations;

/// <summary>One row per problem found in a rejected file.</summary>
internal sealed class ValidationIssueConfiguration : IEntityTypeConfiguration<ValidationIssue>
{
    public void Configure(EntityTypeBuilder<ValidationIssue> builder)
    {
        builder.ToTable("ValidationIssues");

        builder.Property(i => i.TransactionSetControlNumber).HasMaxLength(9);       // ST02
        builder.Property(i => i.Segment).HasMaxLength(3);                           // e.g. NM1
        builder.Property(i => i.LoopId).HasMaxLength(50);
        builder.Property(i => i.Code).HasMaxLength(100);
        builder.Property(i => i.Message).HasMaxLength(2000);
    }
}

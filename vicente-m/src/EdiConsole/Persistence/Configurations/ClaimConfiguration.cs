using EdiConsole.Domain.Claims;
using EdiConsole.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EdiConsole.Persistence.Configurations;

/// <summary>
/// One row per claim. The parties are complex types: their values are columns of the Claims table
/// (e.g. BillingProvider_Npi), not tables of their own. Column sizes follow the X12 element maximums.
/// </summary>
internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");

        builder.Property(c => c.TransactionSetControlNumber).HasMaxLength(9);       // ST02
        builder.Property(c => c.PatientControlNumber).HasMaxLength(38);             // CLM01
        builder.Property(c => c.TotalCharge).HasPrecision(18, 2);                   // CLM02: precision 18 from TotalClaimChargeAmount_02 (R 1–18); 2 decimals for money
        builder.Property(c => c.PlaceOfServiceCode).HasMaxLength(2);                // CLM05-1
        builder.Property(c => c.FrequencyCode).HasMaxLength(1);                     // CLM05-3

        builder.ComplexProperty(c => c.BillingProvider, provider =>                 // 2010AA
        {
            provider.Property(p => p.Npi).HasMaxLength(80);                         // NM109
            provider.Property(p => p.TaxId).HasMaxLength(50);                       // REF02
            provider.ComplexProperty(p => p.Name, ConfigureName);
            provider.ComplexProperty(p => p.Address, ConfigureAddress);
        });

        builder.ComplexProperty(c => c.Subscriber, subscriber =>                    // 2000B + 2010BA
        {
            subscriber.Property(s => s.MemberId).HasMaxLength(80);                  // NM109
            subscriber.Property(s => s.Gender).HasMaxLength(1);                     // DMG03
            subscriber.Property(s => s.PayerResponsibility).HasMaxLength(1);        // SBR01
            subscriber.Property(s => s.GroupNumber).HasMaxLength(50);               // SBR03
            subscriber.Property(s => s.ClaimFilingIndicator).HasMaxLength(2);       // SBR09
            subscriber.ComplexProperty(s => s.Name, ConfigureName);
            subscriber.ComplexProperty(s => s.Address, ConfigureAddress);
        });

        builder.ComplexProperty(c => c.Patient, patient =>                          // 2000C + 2010CA, or the subscriber
        {
            patient.Property(p => p.RelationshipToSubscriber).HasMaxLength(2);      // PAT01, or 18
            patient.Property(p => p.Gender).HasMaxLength(1);                        // DMG03
            patient.ComplexProperty(p => p.Name, ConfigureName);
        });

        builder.ComplexProperty(c => c.Payer, payer =>                              // 2010BB
        {
            payer.Property(p => p.PayerId).HasMaxLength(80);                        // NM109
            payer.Property(p => p.Name).HasMaxLength(60);                           // NM103
        });

        builder.HasMany(c => c.Diagnoses).WithOne().HasForeignKey(d => d.ClaimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.ServiceLines).WithOne().HasForeignKey(l => l.ClaimId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureName(ComplexPropertyBuilder<PartyName> name)        // NM103–NM105
    {
        name.Property(n => n.LastOrOrganizationName).HasMaxLength(60);
        name.Property(n => n.FirstName).HasMaxLength(35);
        name.Property(n => n.MiddleName).HasMaxLength(25);
    }

    private static void ConfigureAddress(ComplexPropertyBuilder<Address> address)    // N3 + N4
    {
        address.Property(a => a.Line1).HasMaxLength(55);
        address.Property(a => a.Line2).HasMaxLength(55);
        address.Property(a => a.City).HasMaxLength(30);
        address.Property(a => a.State).HasMaxLength(2);
        address.Property(a => a.PostalCode).HasMaxLength(15);
    }
}

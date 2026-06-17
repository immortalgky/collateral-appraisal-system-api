using System.Text.Json;
using Auth.Domain.Companies;

namespace Auth.Infrastructure.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies", "auth");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameLocal).HasMaxLength(200);
        builder.Property(c => c.TaxId).HasMaxLength(50);
        builder.Property(c => c.Phone).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.AddressLine1).HasMaxLength(500);
        builder.Property(c => c.AddressLine2).HasMaxLength(500);
        builder.Property(c => c.ContactPerson).HasMaxLength(200);
        builder.Property(c => c.HostCompanyCode).HasMaxLength(10);
        // Legacy "Company Code" from the bank's parameter sheet — persisted for reference only
        // (not maintained or returned by the API).
        builder.Property(c => c.LegacyCompanyCode).HasMaxLength(20);
        builder.Property(c => c.BankAccountNo).HasMaxLength(20);
        builder.Property(c => c.BankAccountName).HasMaxLength(200);

        builder.Property(c => c.LoanTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("nvarchar(max)")
            .HasDefaultValueSql("'[]'");

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasFilter("IsDeleted = 0");
    }
}

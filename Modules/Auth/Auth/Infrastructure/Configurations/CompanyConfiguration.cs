using Auth.Domain.Companies;

namespace Auth.Infrastructure.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies", "auth");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("CompanyId");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TaxId).HasMaxLength(50);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Street).HasMaxLength(500);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.Province).HasMaxLength(100);
        builder.Property(c => c.PostalCode).HasMaxLength(20);

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasFilter("IsDeleted = 0");
    }
}

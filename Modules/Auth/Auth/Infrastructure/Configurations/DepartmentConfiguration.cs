using Auth.Domain.Organization;

namespace Auth.Infrastructure.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments", "auth");

        builder.HasKey(d => d.Code);

        builder.Property(d => d.Code).HasMaxLength(3).IsRequired();
        builder.Property(d => d.DivisionCode).HasMaxLength(3);
        builder.Property(d => d.Description).HasMaxLength(40);
        builder.Property(d => d.IsActive).HasDefaultValue(true);
    }
}

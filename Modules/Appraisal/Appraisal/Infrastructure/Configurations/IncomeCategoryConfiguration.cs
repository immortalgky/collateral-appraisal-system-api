using Appraisal.Domain.Appraisals.Income;

namespace Appraisal.Infrastructure.Configurations;

/// <summary>EF Core configuration for IncomeCategory — child of IncomeSection.</summary>
public class IncomeCategoryConfiguration : IEntityTypeConfiguration<IncomeCategory>
{
    public void Configure(EntityTypeBuilder<IncomeCategory> builder)
    {
        builder.ToTable("IncomeCategories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.IncomeSectionId).IsRequired();
        builder.Property(c => c.CategoryType).IsRequired().HasMaxLength(50);
        builder.Property(c => c.CategoryName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Identifier).IsRequired().HasMaxLength(20);
        builder.Property(c => c.DisplaySeq).IsRequired();
        builder.Property(c => c.TotalCategoryValuesJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");

        // Child assumptions with cascade delete
        builder.HasMany(c => c.Assumptions)
            .WithOne()
            .HasForeignKey(a => a.IncomeCategoryId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.IncomeSectionId);
    }
}

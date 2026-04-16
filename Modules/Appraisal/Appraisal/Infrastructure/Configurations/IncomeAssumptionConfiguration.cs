using Appraisal.Domain.Appraisals.Income;

namespace Appraisal.Infrastructure.Configurations;

/// <summary>EF Core configuration for IncomeAssumption — child of IncomeCategory, owns IncomeMethod.</summary>
public class IncomeAssumptionConfiguration : IEntityTypeConfiguration<IncomeAssumption>
{
    public void Configure(EntityTypeBuilder<IncomeAssumption> builder)
    {
        builder.ToTable("IncomeAssumptions");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.IncomeCategoryId).IsRequired();
        builder.Property(a => a.AssumptionType).IsRequired().HasMaxLength(20);
        builder.Property(a => a.AssumptionName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Identifier).IsRequired().HasMaxLength(20);
        builder.Property(a => a.DisplaySeq).IsRequired();
        builder.Property(a => a.TotalAssumptionValuesJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");

        // Owned IncomeMethod (1:1, flattened into the same row)
        builder.OwnsOne(a => a.Method, m =>
        {
            m.Property(x => x.MethodTypeCode)
                .HasColumnName("Method_MethodTypeCode")
                .IsRequired()
                .HasMaxLength(10);

            m.Property(x => x.DetailJson)
                .HasColumnName("Method_DetailJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("{}");

            m.Property(x => x.TotalMethodValuesJson)
                .HasColumnName("Method_TotalMethodValuesJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");
        });

        builder.HasIndex(a => a.IncomeCategoryId);
    }
}

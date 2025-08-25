using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appraisal.Data.Configurations;

public class RequestAppraisalConfigurations : IEntityTypeConfiguration<RequestAppraisal>
{
    public void Configure(EntityTypeBuilder<RequestAppraisal> builder)
    {
        builder.Property(p => p.Id).HasColumnName("ApprId")
            .UseIdentityColumn();
    }
}
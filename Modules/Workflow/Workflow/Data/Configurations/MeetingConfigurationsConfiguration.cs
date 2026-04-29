using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainMeetingConfiguration = Workflow.Meetings.Domain.MeetingConfiguration;

namespace Workflow.Data.Configurations;

public class MeetingConfigurationsConfiguration : IEntityTypeConfiguration<DomainMeetingConfiguration>
{
    public void Configure(EntityTypeBuilder<DomainMeetingConfiguration> builder)
    {
        builder.ToTable("MeetingConfigurations");

        builder.HasKey(c => c.Key);
        builder.Property(c => c.Key).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Value).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.UpdatedAt).HasColumnType("datetime2").IsRequired();
    }
}

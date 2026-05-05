using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Meetings.Domain;

namespace Workflow.Data.Configurations;

public class MeetingInvitationEmailConfiguration : IEntityTypeConfiguration<MeetingInvitationEmail>
{
    public void Configure(EntityTypeBuilder<MeetingInvitationEmail> builder)
    {
        builder.ToTable("MeetingInvitationEmails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.From).HasMaxLength(500).IsRequired();
        builder.Property(e => e.To).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Content).HasMaxLength(4000);
        builder.Property(e => e.Attachments).HasMaxLength(2000);
        builder.HasIndex(e => e.MeetingId);
    }
}

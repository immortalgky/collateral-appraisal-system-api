using Common.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class LogConfiguration : IEntityTypeConfiguration<Log>
{
    public void Configure(EntityTypeBuilder<Log> builder)
    {
        builder.ToTable("Logs", "dbo");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TimeStamp)
            .HasColumnType("datetime2(3)");

        builder.Property(x => x.Level)
            .HasColumnType("nvarchar(16)");

        builder.Property(x => x.Message)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Exception)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Properties)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CorrelationId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.EntityId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.AppraisalId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.RequestId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.WorkflowInstanceId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.CollateralId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.DocumentId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.MachineName)
            .HasColumnType("nvarchar(128)");

        builder.Property(x => x.UserName)
            .HasColumnType("nvarchar(128)");

        builder.Property(x => x.SourceContext)
            .HasColumnType("nvarchar(256)");

        builder.Property(x => x.RequestPath)
            .HasColumnType("nvarchar(400)");

        builder.Property(x => x.MessageTemplate)
            .HasColumnType("nvarchar(max)");

        // INCLUDE(Level) lets the summary histogram (GROUP BY over TimeStamp, aggregating by Level)
        // seek this index directly and read Level from the leaf, instead of scanning the whole
        // (Level, TimeStamp) index every time regardless of window size (Level is that index's
        // leading key, so a TimeStamp-only predicate can't seek it).
        builder.HasIndex(x => x.TimeStamp)
            .IncludeProperties(x => x.Level)
            .HasDatabaseName("IX_Logs_TimeStamp");

        // Not redundant with IX_Logs_TimeStamp above despite the overlapping columns: this one has
        // Level as the LEADING key, so it seeks directly for a level-filtered search over a wide
        // window (e.g. "level:Error" over 30 days) and for the top-problems query (Level IN
        // ('Warning','Error','Fatal')), neither of which IX_Logs_TimeStamp can seek on — Level is
        // only in its INCLUDE, not its key, so a level predicate there means scanning every row in
        // the TimeStamp range regardless of Level.
        builder.HasIndex(x => new { x.Level, x.TimeStamp })
            .HasDatabaseName("IX_Logs_Level_TimeStamp");

        builder.HasIndex(x => new { x.UserName, x.TimeStamp })
            .HasFilter("[UserName] IS NOT NULL")
            .HasDatabaseName("IX_Logs_UserName_TimeStamp");

        builder.HasIndex(x => x.AppraisalId)
            .HasFilter("[AppraisalId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_AppraisalId");

        builder.HasIndex(x => x.RequestId)
            .HasFilter("[RequestId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_RequestId");

        builder.HasIndex(x => x.EntityId)
            .HasFilter("[EntityId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_EntityId");

        builder.HasIndex(x => x.CorrelationId)
            .HasFilter("[CorrelationId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_CorrelationId");

        builder.HasIndex(x => x.WorkflowInstanceId)
            .HasFilter("[WorkflowInstanceId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_WorkflowInstanceId");

        builder.HasIndex(x => x.CollateralId)
            .HasFilter("[CollateralId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_CollateralId");

        builder.HasIndex(x => x.DocumentId)
            .HasFilter("[DocumentId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_DocumentId");
    }
}

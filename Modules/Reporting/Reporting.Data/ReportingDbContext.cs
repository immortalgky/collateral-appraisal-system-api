using Microsoft.EntityFrameworkCore;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Reporting.Data;

/// <summary>
/// Write-side context for the Reporting module. The module is otherwise read-only (Dapper over
/// views); this context owns reporting-specific tables — currently the report-generation
/// audit log and the report-definition config table. Its EF migration creates the `reporting`
/// schema (EnsureSchema), so the schema exists before the repeatable view scripts run.
///
/// Lives in the light Reporting.Data project (Shared + EF only) so the Database migration host can
/// reference it without pulling in the module's render engines (PuppeteerSharp/ClosedXML/PdfSharp).
/// </summary>
public class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<ReportGenerationLog> ReportGenerationLogs => Set<ReportGenerationLog>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("reporting");

        modelBuilder.Entity<ReportGenerationLog>(b =>
        {
            b.ToTable("ReportGenerationLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.ReportName).IsRequired().HasMaxLength(100);
            b.Property(x => x.Format).IsRequired().HasMaxLength(10);
            b.Property(x => x.GeneratedBy).HasMaxLength(50);
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
            b.HasIndex(x => x.GeneratedAt);
            b.HasIndex(x => x.ReportName);
        });

        modelBuilder.Entity<ReportDefinition>(b =>
        {
            b.ToTable("ReportDefinitions");
            b.HasKey(x => x.ReportTypeKey);
            b.Property(x => x.ReportTypeKey).IsRequired().HasMaxLength(100);
            b.Property(x => x.TemplateId).IsRequired().HasMaxLength(100);
            b.Property(x => x.DisplayNameTh).IsRequired().HasMaxLength(200);
            b.Property(x => x.DisplayNameEn).IsRequired().HasMaxLength(200);
            b.Property(x => x.Category).IsRequired().HasMaxLength(50);
            b.Property(x => x.GenerationMode)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(10);
            b.Property(x => x.IsEnabled).IsRequired();
            b.Property(x => x.Version).IsRequired();

            b.HasData(SeedData());
        });

        modelBuilder.Entity<ReportJob>(b =>
        {
            b.ToTable("ReportJobs");
            b.HasKey(x => x.Id);
            b.Property(x => x.ReportTypeKey).IsRequired().HasMaxLength(100);
            b.Property(x => x.EntityId).IsRequired().HasMaxLength(100);
            b.Property(x => x.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);
            b.Property(x => x.RequestedBy).IsRequired().HasMaxLength(50);
            b.Property(x => x.StoragePath).HasMaxLength(500);
            b.Property(x => x.FileName).HasMaxLength(200);
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
            b.HasIndex(x => x.RequestedBy);
            b.HasIndex(x => x.RequestedAt);
        });

        // Transactional outbox (+ inbox + background-service lease) in the `reporting` schema.
        // Lets ReportGenerationJob enqueue completion events in the SAME transaction as the
        // ReportJobs status update, so the DispatchDomainEventInterceptor drains them atomically
        // and IntegrationEventDeliveryService<ReportingDbContext> delivers them exactly-once.
        modelBuilder.AddIntegrationEventOutbox();

        // Per-module recurring-job schedule table (reporting.JobSchedules)
        modelBuilder.AddJobSchedules();

        base.OnModelCreating(modelBuilder);
    }

    private static ReportDefinition[] SeedData() =>
    [
        ReportDefinition.Create(
            reportTypeKey: "appointment-letter",
            templateId: "appointment-letter",
            displayNameTh: "หนังสือนัดหมาย",
            displayNameEn: "Appointment Letter",
            category: "Appointment",
            generationMode: ReportGenerationMode.Sync),

        // Unified entry point — composite provider auto-picks the per-property summary forms
        // (block-only / construction-only / else land-building + condo + machine) and merges
        // them into one PDF. The 5 per-property sub-forms (appraisal-summary-{land-building,
        // condo,machine,construction,block}) are NOT seeded here on purpose: they are internal
        // building blocks resolved by AppraisalSummaryDataProvider via the ReportRegistry
        // provider fallback (their IReportDataProviders stay registered in ReportingModule;
        // TemplateId == key loads their templates). Keeping them out of ReportDefinitions hides
        // them from GET /reports/definitions so only the unified entry is user-selectable.
        // TemplateId below is unused (the composite renders no template of its own).
        ReportDefinition.Create(
            reportTypeKey: "appraisal-summary",
            templateId: "appraisal-summary",
            displayNameTh: "สรุปรายงานการประเมิน",
            displayNameEn: "Appraisal Summary",
            category: "AppraisalSummary",
            generationMode: ReportGenerationMode.Async),

        // Unified appraisal book — one report (template appraisal-book.html) auto-detects
        // internal vs external (AppraisalAssignments.AssignmentType) and the body type
        // (standard / construction / block), replacing the former external-appraisal-report,
        // internal-report-construction and internal-report-block definitions.
        ReportDefinition.Create(
            reportTypeKey: "appraisal-book",
            templateId: "appraisal-book",
            displayNameTh: "เล่มรายงานการประเมิน",
            displayNameEn: "Appraisal Book",
            category: "AppraisalBook",
            generationMode: ReportGenerationMode.Async),

        ReportDefinition.Create(
            reportTypeKey: "meeting-invitation",
            templateId: "meeting-invitation",
            displayNameTh: "หนังสือเชิญประชุม",
            displayNameEn: "Meeting Invitation",
            category: "Meeting",
            generationMode: ReportGenerationMode.Sync),

        ReportDefinition.Create(
            reportTypeKey: "meeting-minute",
            templateId: "meeting-minute",
            displayNameTh: "รายงานการประชุม",
            displayNameEn: "Meeting Minute",
            category: "Meeting",
            generationMode: ReportGenerationMode.Sync),
    ];
}

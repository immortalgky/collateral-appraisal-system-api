using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "appraisal");

            migrationBuilder.CreateTable(
                name: "AdjustmentTypeLookups",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AdjustmentCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TypicalMinPercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    TypicalMaxPercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ApplicablePropertyTypes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjustmentTypeLookups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalComparables",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    OriginalPricePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustedPricePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAdjustmentPct = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    WeightedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SelectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalComparables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalGallery",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhotoNumber = table.Column<int>(type: "int", nullable: false),
                    PhotoType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhotoCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsedInReport = table.Column<bool>(type: "bit", nullable: false),
                    ReportSection = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalGallery", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalReviews",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewSequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignedTo = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalVotes = table.Column<int>(type: "int", nullable: true),
                    VotesApprove = table.Column<int>(type: "int", nullable: true),
                    VotesReject = table.Column<int>(type: "int", nullable: true),
                    VotesAbstain = table.Column<int>(type: "int", nullable: true),
                    MeetingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MeetingReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appraisals",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AppraisalType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SLADays = table.Column<int>(type: "int", nullable: true),
                    SLADueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SLAStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ActualDaysToComplete = table.Column<int>(type: "int", nullable: true),
                    IsWithinSLA = table.Column<bool>(type: "bit", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appraisals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalSettings",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoAssignmentRules",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PropertyTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Provinces = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MinEstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxEstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LoanTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priorities = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssignmentMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssignToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignToTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignToCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoAssignmentRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingAppraisalSurfaces",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromFloorNo = table.Column<int>(type: "int", nullable: false),
                    ToFloorNo = table.Column<int>(type: "int", nullable: false),
                    FloorType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FloorStructure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FloorStructureOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FloorSurface = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FloorSurfaceOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingAppraisalSurfaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingDepreciationDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepreciationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UsefulLifeYears = table.Column<int>(type: "int", nullable: false),
                    EffectiveAge = table.Column<int>(type: "int", nullable: false),
                    RemainingLifeYears = table.Column<int>(type: "int", nullable: false),
                    SalvageValuePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ReplacementCostNew = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PhysicalDepreciationPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PhysicalDepreciationAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FunctionalObsolescencePct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    FunctionalObsolescenceAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExternalObsolescencePct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ExternalObsolescenceAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalDepreciationPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TotalDepreciationAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepreciatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StructuralCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaintenanceLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConditionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingDepreciationDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Committees",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CommitteeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CommitteeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    QuorumType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuorumValue = table.Column<int>(type: "int", nullable: false),
                    MajorityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Committees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeVotes",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MemberRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Vote = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeVotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComparativeAnalysisTemplates",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparativeAnalysisTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CondoAppraisalAreaDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AreaSize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoAppraisalAreaDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructures",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FeeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "LawAndRegulations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeaderCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawAndRegulations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FactorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FactorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FieldLength = table.Column<int>(type: "int", nullable: true),
                    FieldDecimal = table.Column<int>(type: "int", nullable: true),
                    ParameterGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableFactors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableTemplates",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "appraisal",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "PricingAnalysis",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    FinalAppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingAnalysis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyPhotoMappings",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    GalleryPhotoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyDetailType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PropertyDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhotoPurpose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SectionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    LinkedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyPhotoMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuotationRequests",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    QuotationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAppraisals = table.Column<int>(type: "int", nullable: false),
                    RequestDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SpecialRequirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    TotalCompaniesInvited = table.Column<int>(type: "int", nullable: false),
                    TotalQuotationsReceived = table.Column<int>(type: "int", nullable: false),
                    SelectedCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SelectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValuationAnalyses",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationApproach = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ValuationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ForcedSaleValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InsuranceValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AppraiserOpinion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValuationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComparableAdjustments",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdjustmentPercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    AdjustmentDirection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ComparableValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparableAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComparableAdjustments_AppraisalComparables_AppraisalComparableId",
                        column: x => x.AppraisalComparableId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalComparables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalAssignments",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignmentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssigneeCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalAppraiserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalAppraiserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InternalAppraiserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InternalAppraiserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssignmentSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AutoRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuotationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReassignmentNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastProgressUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalAssignments_AppraisalAssignments_PreviousAssignmentId",
                        column: x => x.PreviousAssignmentId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalAssignments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppraisalAssignments_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalProperties",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalProperties_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyGroups",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupNumber = table.Column<int>(type: "int", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UseSystemCalc = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyGroups_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeApprovalConditions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConditionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoleRequired = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MinVotesRequired = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeApprovalConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeApprovalConditions_Committees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalSchema: "appraisal",
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeMembers",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_Committees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalSchema: "appraisal",
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComparativeAnalysisTemplateFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DefaultWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparativeAnalysisTemplateFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComparativeAnalysisTemplateFactors_ComparativeAnalysisTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "appraisal",
                        principalTable: "ComparativeAnalysisTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRequirements",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollateralTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRequirements_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "appraisal",
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LawAndRegulationImages",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    LawAndRegulationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawAndRegulationImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawAndRegulationImages_LawAndRegulations_LawAndRegulationId",
                        column: x => x.LawAndRegulationId,
                        principalSchema: "appraisal",
                        principalTable: "LawAndRegulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparables",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ComparableNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PricePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DataSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataConfidence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SurveyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SurveyedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketComparables_MarketComparableTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableTemplateFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableTemplateFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketComparableTemplateFactors_MarketComparableFactors_FactorId",
                        column: x => x.FactorId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketComparableTemplateFactors_MarketComparableTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "appraisal",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "appraisal",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "appraisal",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "PricingAnalysisApproaches",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproachType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApproachValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsSelected = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingAnalysisApproaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingAnalysisApproaches_PricingAnalysis_PricingAnalysisId",
                        column: x => x.PricingAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyQuotations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    QuotationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalQuotedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "THB"),
                    EstimatedDays = table.Column<int>(type: "int", nullable: false),
                    ProposedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProposedCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Submitted"),
                    IsWinner = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedByEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedByPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyQuotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyQuotations_QuotationRequests_QuotationRequestId",
                        column: x => x.QuotationRequestId,
                        principalSchema: "appraisal",
                        principalTable: "QuotationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationInvitations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    QuotationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotificationSent = table.Column<bool>(type: "bit", nullable: false),
                    NotificationSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationInvitations_QuotationRequests_QuotationRequestId",
                        column: x => x.QuotationRequestId,
                        principalSchema: "appraisal",
                        principalTable: "QuotationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationRequestItems",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    QuotationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemNumber = table.Column<int>(type: "int", nullable: false),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PropertyLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ItemNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecialRequirements = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationRequestItems_QuotationRequests_QuotationRequestId",
                        column: x => x.QuotationRequestId,
                        principalSchema: "appraisal",
                        principalTable: "QuotationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupValuations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ValuationAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ForcedSaleValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValuePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValuationWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ValuationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupValuations_ValuationAnalyses_ValuationAnalysisId",
                        column: x => x.ValuationAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "ValuationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyValuations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ValuationAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyDetailType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PropertyDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ForcedSaleValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValuePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValuationWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ValuationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyValuations_ValuationAnalyses_ValuationAnalysisId",
                        column: x => x.ValuationAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "ValuationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProposedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LocationDetail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RescheduleCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AppointedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_AppraisalAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalAssignments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppraisalFees",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalFeeBeforeVAT = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    VATRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 7.00m),
                    VATAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    TotalFeeAfterVAT = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    BankAbsorbAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    CustomerPayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    TotalPaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    InspectionFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalFees_AppraisalAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildingAppraisalDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuiltOnTitleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BuildingConditionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsUnderConstruction = table.Column<bool>(type: "bit", nullable: true),
                    ConstructionCompletionPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ConstructionLicenseExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAppraisable = table.Column<bool>(type: "bit", nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfFloors = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsEncroachingOthers = table.Column<bool>(type: "bit", nullable: true),
                    EncroachingOthersRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EncroachingOthersArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BuildingMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsResidential = table.Column<bool>(type: "bit", nullable: true),
                    BuildingAge = table.Column<int>(type: "int", nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    ResidentialRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConstructionStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionStyleRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StructureType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    StructureTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofFrameType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofFrameTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CeilingType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CeilingTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    InteriorWallTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    ExteriorWallTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FenceType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    FenceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConstructionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UtilizationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UtilizationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalBuildingArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BuildingInsurancePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForcedSalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingAppraisalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingAppraisalDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoAppraisalDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CondoName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuiltOnTitleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FloorNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhysicalFloorNumber = table.Column<int>(type: "int", nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: true),
                    BuildingConditionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDocumentValidated = table.Column<bool>(type: "bit", nullable: true),
                    LocationType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DistanceFromMainRoad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AccessRoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RightOfWay = table.Column<short>(type: "smallint", nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicUtilityType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PublicUtilityTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BuildingAge = table.Column<int>(type: "int", nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    NumberOfFloors = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    BuildingFormType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomLayoutType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomLayoutTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LocationViewType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    GroundFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpperFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpperFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BathroomFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BathroomFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalBuildingArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationLineRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FacilityType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    FacilityTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EnvironmentType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    BuildingInsurancePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForcedSalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoAppraisalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoAppraisalDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandAppraisalDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsLandLocationVerified = table.Column<bool>(type: "bit", nullable: true),
                    LandCheckMethodType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandCheckMethodTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DistanceFromMainRoad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Village = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LandShapeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UrbanPlanningType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandZoneType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PlotLocationType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PlotLocationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandFillType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandFillTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandFillPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SoilLevel = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AccessRoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RightOfWay = table.Column<short>(type: "smallint", nullable: true),
                    RoadFrontage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    NumberOfSidesFacingRoad = table.Column<int>(type: "int", nullable: true),
                    RoadPassInFrontOfLand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandAccessibilityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandAccessibilityRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HasElectricity = table.Column<bool>(type: "bit", nullable: true),
                    ElectricityDistance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PublicUtilityType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PublicUtilityTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandUseType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandUseTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandEntranceExitType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandEntranceExitTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransportationAccessType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    TransportationAccessTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PropertyAnticipationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationLineRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEncroached = table.Column<bool>(type: "bit", nullable: true),
                    EncroachmentRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EncroachmentArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsLandlocked = table.Column<bool>(type: "bit", nullable: true),
                    LandlockedRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OtherLegalLimitations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvictionType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    EvictionTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AllocationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NorthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NorthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SouthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SouthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EastAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EastBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    WestAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WestBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PondArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PondDepth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    HasBuilding = table.Column<bool>(type: "bit", nullable: true),
                    HasBuildingOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandAppraisalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAppraisalDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MachineryAppraisalDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EngineNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChassisNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: true),
                    CountryOfManufacture = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Capacity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EnergyUse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnergyUseRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: false),
                    CanUse = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConditionUse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MachineCondition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MachineAge = table.Column<int>(type: "int", nullable: true),
                    MachineEfficiency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MachineTechnology = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsePurpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MachinePart = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Other = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppraiserOpinion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryAppraisalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineryAppraisalDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleAppraisalDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VehicleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EngineNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChassisNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: true),
                    CountryOfManufacture = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Capacity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EnergyUse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnergyUseRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: false),
                    CanUse = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConditionUse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleCondition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleAge = table.Column<int>(type: "int", nullable: true),
                    VehicleEfficiency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleTechnology = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsePurpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VehiclePart = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Other = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppraiserOpinion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAppraisalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleAppraisalDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VesselAppraisalDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VesselName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EngineNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: true),
                    PlaceOfManufacture = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VesselType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClassOfVessel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EngineCapacity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    GrossTonnage = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NetTonnage = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EnergyUse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnergyUseRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: false),
                    CanUse = table.Column<bool>(type: "bit", nullable: false),
                    FormerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VesselCurrentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConditionUse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VesselCondition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VesselAge = table.Column<int>(type: "int", nullable: true),
                    VesselEfficiency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VesselTechnology = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsePurpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VesselPart = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Other = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppraiserOpinion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VesselAppraisalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VesselAppraisalDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyGroupItems",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceInGroup = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyGroupItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyGroupItems_PropertyGroups_PropertyGroupId",
                        column: x => x.PropertyGroupId,
                        principalSchema: "appraisal",
                        principalTable: "PropertyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableData",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtherRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketComparableData_MarketComparableFactors_FactorId",
                        column: x => x.FactorId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketComparableData_MarketComparables_MarketComparableId",
                        column: x => x.MarketComparableId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketComparableImages",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketComparableImages_MarketComparables_MarketComparableId",
                        column: x => x.MarketComparableId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingAnalysisMethods",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ApproachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MethodValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValuePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsSelected = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingAnalysisMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingAnalysisMethods_PricingAnalysisApproaches_ApproachId",
                        column: x => x.ApproachId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisApproaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyQuotationItems",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CompanyQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotationRequestItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemNumber = table.Column<int>(type: "int", nullable: false),
                    QuotedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "THB"),
                    PriceBreakdown = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstimatedDays = table.Column<int>(type: "int", nullable: false),
                    ProposedCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ItemNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalQuotedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentNegotiatedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NegotiationRounds = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyQuotationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyQuotationItems_CompanyQuotations_CompanyQuotationId",
                        column: x => x.CompanyQuotationId,
                        principalSchema: "appraisal",
                        principalTable: "CompanyQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationNegotiations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CompanyQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NegotiationRound = table.Column<int>(type: "int", nullable: false),
                    InitiatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CounterPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CounterTimeline = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationNegotiations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationNegotiations_CompanyQuotations_CompanyQuotationId",
                        column: x => x.CompanyQuotationId,
                        principalSchema: "appraisal",
                        principalTable: "CompanyQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentHistory",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousAppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreviousLocationDetail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentHistory_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "appraisal",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalFeeItems",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalFeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FeeDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalFeeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalFeeItems_AppraisalFees_AppraisalFeeId",
                        column: x => x.AppraisalFeeId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalFees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalFeePaymentHistory",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalFeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalFeePaymentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalFeePaymentHistory_AppraisalFees_AppraisalFeeId",
                        column: x => x.AppraisalFeeId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalFees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandTitles",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    LandAppraisalDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleDeedNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleDeedType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BookNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PageNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandParcelNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SurveyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MapSheetNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Rawang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AerialMapName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AerialMapNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AreaRai = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AreaNgan = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AreaSquareWa = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    HasBoundaryMarker = table.Column<bool>(type: "bit", nullable: true),
                    BoundaryMarkerRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDocumentValidated = table.Column<bool>(type: "bit", nullable: true),
                    IsMissingFromSurvey = table.Column<bool>(type: "bit", nullable: true),
                    GovernmentPricePerSqWa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GovernmentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandTitles_LandAppraisalDetails_LandAppraisalDetailId",
                        column: x => x.LandAppraisalDetailId,
                        principalSchema: "appraisal",
                        principalTable: "LandAppraisalDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingCalculations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OfferingPriceUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AdjustOfferPricePct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AdjustOfferPriceAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SellingPriceUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BuySellYear = table.Column<int>(type: "int", nullable: true),
                    BuySellMonth = table.Column<int>(type: "int", nullable: true),
                    AdjustedPeriodPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CumulativeAdjPeriod = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandAreaDeficient = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LandAreaDeficientUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LandPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LandValueAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaDeficient = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaDeficientUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UsableAreaPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BuildingValueAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalFactorDiffPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    TotalFactorDiffAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAdjustedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingCalculations_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingComparableLinks",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingComparableLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingComparableLinks_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingComparativeFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    IsSelectedForScoring = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingComparativeFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingComparativeFactors_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingFactorScores",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketComparableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    WeightedScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AdjustmentPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingFactorScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingFactorScores_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingFinalValues",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalValueRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludeLandArea = table.Column<bool>(type: "bit", nullable: false),
                    LandArea = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AppraisalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AppraisalPriceRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceDifferentiate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    HasBuildingCost = table.Column<bool>(type: "bit", nullable: false),
                    BuildingCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AppraisalPriceWithBuilding = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AppraisalPriceWithBuildingRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingFinalValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingFinalValues_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "appraisal",
                table: "FeeStructures",
                columns: new[] { "Id", "BaseAmount", "CreatedAt", "CreatedBy", "CreatedWorkstation", "FeeCode", "FeeName", "IsActive", "UpdatedAt", "UpdatedBy", "UpdatedWorkstation" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), 0m, null, "System", null, "01", "Appraisal Fee", true, null, null, null },
                    { new Guid("00000000-0000-0000-0000-000000000002"), 0m, null, "System", null, "02", "Travel Fee", true, null, null, null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), 0m, null, "System", null, "03", "Urgent Fee", true, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentTypeLookups_AdjustmentCategory",
                schema: "appraisal",
                table: "AdjustmentTypeLookups",
                column: "AdjustmentCategory");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentTypeLookups_AdjustmentCategory_AdjustmentType",
                schema: "appraisal",
                table: "AdjustmentTypeLookups",
                columns: new[] { "AdjustmentCategory", "AdjustmentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHistory_AppointmentId",
                schema: "appraisal",
                table: "AppointmentHistory",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentDateTime",
                schema: "appraisal",
                table: "Appointments",
                column: "AppointmentDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AssignmentId",
                schema: "appraisal",
                table: "Appointments",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status",
                schema: "appraisal",
                table: "Appointments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAssignments_AppraisalId",
                schema: "appraisal",
                table: "AppraisalAssignments",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAssignments_AssigneeCompanyId",
                schema: "appraisal",
                table: "AppraisalAssignments",
                column: "AssigneeCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAssignments_AssigneeUserId",
                schema: "appraisal",
                table: "AppraisalAssignments",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAssignments_PreviousAssignmentId",
                schema: "appraisal",
                table: "AppraisalAssignments",
                column: "PreviousAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalComparables_AppraisalId",
                schema: "appraisal",
                table: "AppraisalComparables",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalComparables_AppraisalId_SequenceNumber",
                schema: "appraisal",
                table: "AppraisalComparables",
                columns: new[] { "AppraisalId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalComparables_MarketComparableId",
                schema: "appraisal",
                table: "AppraisalComparables",
                column: "MarketComparableId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalFeeItems_AppraisalFeeId",
                schema: "appraisal",
                table: "AppraisalFeeItems",
                column: "AppraisalFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalFeePaymentHistory_AppraisalFeeId",
                schema: "appraisal",
                table: "AppraisalFeePaymentHistory",
                column: "AppraisalFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalFees_AssignmentId",
                schema: "appraisal",
                table: "AppraisalFees",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalFees_PaymentStatus",
                schema: "appraisal",
                table: "AppraisalFees",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGallery_AppraisalId",
                schema: "appraisal",
                table: "AppraisalGallery",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGallery_AppraisalId_PhotoNumber",
                schema: "appraisal",
                table: "AppraisalGallery",
                columns: new[] { "AppraisalId", "PhotoNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGallery_DocumentId",
                schema: "appraisal",
                table: "AppraisalGallery",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalProperties_AppraisalId",
                schema: "appraisal",
                table: "AppraisalProperties",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalProperties_AppraisalId_SequenceNumber",
                schema: "appraisal",
                table: "AppraisalProperties",
                columns: new[] { "AppraisalId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_AppraisalId",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_AssignedTo",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_CommitteeId",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "CommitteeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_TeamId",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_AppraisalNumber",
                schema: "appraisal",
                table: "Appraisals",
                column: "AppraisalNumber",
                unique: true,
                filter: "[AppraisalNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_RequestId",
                schema: "appraisal",
                table: "Appraisals",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSettings_SettingKey",
                schema: "appraisal",
                table: "AppraisalSettings",
                column: "SettingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutoAssignmentRules_IsActive",
                schema: "appraisal",
                table: "AutoAssignmentRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AutoAssignmentRules_Priority",
                schema: "appraisal",
                table: "AutoAssignmentRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingAppraisalDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingAppraisalSurfaces_AppraisalPropertyId",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                column: "AppraisalPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingDepreciationDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                column: "AppraisalPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeApprovalConditions_CommitteeId",
                schema: "appraisal",
                table: "CommitteeApprovalConditions",
                column: "CommitteeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_CommitteeId",
                schema: "appraisal",
                table: "CommitteeMembers",
                column: "CommitteeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_CommitteeId_UserId",
                schema: "appraisal",
                table: "CommitteeMembers",
                columns: new[] { "CommitteeId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_UserId",
                schema: "appraisal",
                table: "CommitteeMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_CommitteeCode",
                schema: "appraisal",
                table: "Committees",
                column: "CommitteeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeVotes_CommitteeMemberId",
                schema: "appraisal",
                table: "CommitteeVotes",
                column: "CommitteeMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeVotes_ReviewId",
                schema: "appraisal",
                table: "CommitteeVotes",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQuotationItems_AppraisalId",
                schema: "appraisal",
                table: "CompanyQuotationItems",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQuotationItems_CompanyQuotationId",
                schema: "appraisal",
                table: "CompanyQuotationItems",
                column: "CompanyQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQuotations_CompanyId",
                schema: "appraisal",
                table: "CompanyQuotations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQuotations_QuotationRequestId",
                schema: "appraisal",
                table: "CompanyQuotations",
                column: "QuotationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQuotations_Status",
                schema: "appraisal",
                table: "CompanyQuotations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComparableAdjustments_AppraisalComparableId",
                schema: "appraisal",
                table: "ComparableAdjustments",
                column: "AppraisalComparableId");

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplateFactors_TemplateId",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplateFactors",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplateFactors_TemplateId_FactorId",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplateFactors",
                columns: new[] { "TemplateId", "FactorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplates_PropertyType",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplates",
                column: "PropertyType");

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplates_TemplateCode",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplates",
                column: "TemplateCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalAreaDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "CondoAppraisalAreaDetails",
                column: "AppraisalPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_CollateralTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                column: "CollateralTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_DocumentTypeId_CollateralTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                columns: new[] { "DocumentTypeId", "CollateralTypeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_IsActive",
                schema: "appraisal",
                table: "DocumentRequirements",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                schema: "appraisal",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_FeeCode",
                schema: "appraisal",
                table: "FeeStructures",
                column: "FeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupValuations_PropertyGroupId",
                schema: "appraisal",
                table: "GroupValuations",
                column: "PropertyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupValuations_ValuationAnalysisId",
                schema: "appraisal",
                table: "GroupValuations",
                column: "ValuationAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "appraisal",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_LandAppraisalDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandTitles_LandAppraisalDetailId",
                schema: "appraisal",
                table: "LandTitles",
                column: "LandAppraisalDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_LawAndRegulationImages_LawAndRegulationId",
                schema: "appraisal",
                table: "LawAndRegulationImages",
                column: "LawAndRegulationId");

            migrationBuilder.CreateIndex(
                name: "IX_LawAndRegulations_AppraisalId",
                schema: "appraisal",
                table: "LawAndRegulations",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryAppraisalDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableData_FactorId",
                schema: "appraisal",
                table: "MarketComparableData",
                column: "FactorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableData_MarketComparableId",
                schema: "appraisal",
                table: "MarketComparableData",
                column: "MarketComparableId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableData_MarketComparableId_FactorId",
                schema: "appraisal",
                table: "MarketComparableData",
                columns: new[] { "MarketComparableId", "FactorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableFactors_FactorCode",
                schema: "appraisal",
                table: "MarketComparableFactors",
                column: "FactorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableFactors_IsActive",
                schema: "appraisal",
                table: "MarketComparableFactors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableImages_DocumentId",
                schema: "appraisal",
                table: "MarketComparableImages",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableImages_MarketComparableId",
                schema: "appraisal",
                table: "MarketComparableImages",
                column: "MarketComparableId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableImages_MarketComparableId_DisplaySequence",
                schema: "appraisal",
                table: "MarketComparableImages",
                columns: new[] { "MarketComparableId", "DisplaySequence" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables",
                column: "ComparableNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_PropertyType",
                schema: "appraisal",
                table: "MarketComparables",
                column: "PropertyType");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_Province",
                schema: "appraisal",
                table: "MarketComparables",
                column: "Province");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_Status",
                schema: "appraisal",
                table: "MarketComparables",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_TemplateId",
                schema: "appraisal",
                table: "MarketComparables",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplateFactors_FactorId",
                schema: "appraisal",
                table: "MarketComparableTemplateFactors",
                column: "FactorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplateFactors_TemplateId",
                schema: "appraisal",
                table: "MarketComparableTemplateFactors",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplateFactors_TemplateId_FactorId",
                schema: "appraisal",
                table: "MarketComparableTemplateFactors",
                columns: new[] { "TemplateId", "FactorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplates_IsActive",
                schema: "appraisal",
                table: "MarketComparableTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplates_PropertyType",
                schema: "appraisal",
                table: "MarketComparableTemplates",
                column: "PropertyType");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableTemplates_TemplateCode",
                schema: "appraisal",
                table: "MarketComparableTemplates",
                column: "TemplateCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "appraisal",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "appraisal",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "appraisal",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true,
                filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "appraisal",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true,
                filter: "[OutboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "appraisal",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysis_PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis",
                column: "PropertyGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysisApproaches_PricingAnalysisId",
                schema: "appraisal",
                table: "PricingAnalysisApproaches",
                column: "PricingAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysisMethods_ApproachId",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                column: "ApproachId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingCalculations_PricingMethodId",
                schema: "appraisal",
                table: "PricingCalculations",
                column: "PricingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingComparableLinks_PricingMethodId_MarketComparableId",
                schema: "appraisal",
                table: "PricingComparableLinks",
                columns: new[] { "PricingMethodId", "MarketComparableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingComparativeFactors_PricingMethodId",
                schema: "appraisal",
                table: "PricingComparativeFactors",
                column: "PricingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingComparativeFactors_PricingMethodId_FactorId",
                schema: "appraisal",
                table: "PricingComparativeFactors",
                columns: new[] { "PricingMethodId", "FactorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingFactorScores_PricingMethodId",
                schema: "appraisal",
                table: "PricingFactorScores",
                column: "PricingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingFactorScores_PricingMethodId_MarketComparableId_FactorId",
                schema: "appraisal",
                table: "PricingFactorScores",
                columns: new[] { "PricingMethodId", "MarketComparableId", "FactorId" },
                unique: true,
                filter: "[MarketComparableId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PricingFinalValues_PricingMethodId",
                schema: "appraisal",
                table: "PricingFinalValues",
                column: "PricingMethodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupItems_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyGroupItems",
                column: "AppraisalPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupItems_PropertyGroupId",
                schema: "appraisal",
                table: "PropertyGroupItems",
                column: "PropertyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupItems_PropertyGroupId_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyGroupItems",
                columns: new[] { "PropertyGroupId", "AppraisalPropertyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroups_AppraisalId",
                schema: "appraisal",
                table: "PropertyGroups",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroups_AppraisalId_GroupNumber",
                schema: "appraisal",
                table: "PropertyGroups",
                columns: new[] { "AppraisalId", "GroupNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotoMappings_GalleryPhotoId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                column: "GalleryPhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotoMappings_PropertyDetailType_PropertyDetailId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                columns: new[] { "PropertyDetailType", "PropertyDetailId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValuations_PropertyDetailType_PropertyDetailId",
                schema: "appraisal",
                table: "PropertyValuations",
                columns: new[] { "PropertyDetailType", "PropertyDetailId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValuations_ValuationAnalysisId",
                schema: "appraisal",
                table: "PropertyValuations",
                column: "ValuationAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationInvitations_CompanyId",
                schema: "appraisal",
                table: "QuotationInvitations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationInvitations_QuotationRequestId",
                schema: "appraisal",
                table: "QuotationInvitations",
                column: "QuotationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationInvitations_Status",
                schema: "appraisal",
                table: "QuotationInvitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationNegotiations_CompanyQuotationId",
                schema: "appraisal",
                table: "QuotationNegotiations",
                column: "CompanyQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationNegotiations_QuotationItemId",
                schema: "appraisal",
                table: "QuotationNegotiations",
                column: "QuotationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationNegotiations_Status",
                schema: "appraisal",
                table: "QuotationNegotiations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequestItems_AppraisalId",
                schema: "appraisal",
                table: "QuotationRequestItems",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequestItems_QuotationRequestId",
                schema: "appraisal",
                table: "QuotationRequestItems",
                column: "QuotationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_DueDate",
                schema: "appraisal",
                table: "QuotationRequests",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_QuotationNumber",
                schema: "appraisal",
                table: "QuotationRequests",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_Status",
                schema: "appraisal",
                table: "QuotationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationAnalyses_AppraisalId",
                schema: "appraisal",
                table: "ValuationAnalyses",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAppraisalDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VesselAppraisalDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "VesselAppraisalDetails",
                column: "AppraisalPropertyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjustmentTypeLookups",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppointmentHistory",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalFeeItems",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalFeePaymentHistory",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalGallery",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalReviews",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalSettings",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AutoAssignmentRules",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "BuildingAppraisalDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "BuildingAppraisalSurfaces",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "BuildingDepreciationDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CommitteeApprovalConditions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CommitteeMembers",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CommitteeVotes",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CompanyQuotationItems",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ComparableAdjustments",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ComparativeAnalysisTemplateFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoAppraisalAreaDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoAppraisalDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "DocumentRequirements",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "FeeStructures",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "GroupValuations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "LandTitles",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "LawAndRegulationImages",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MachineryAppraisalDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableData",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableImages",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableTemplateFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingCalculations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingComparableLinks",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingComparativeFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingFactorScores",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingFinalValues",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PropertyGroupItems",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PropertyPhotoMappings",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PropertyValuations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "QuotationInvitations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "QuotationNegotiations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "QuotationRequestItems",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VehicleAppraisalDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VesselAppraisalDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "Appointments",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalFees",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "Committees",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalComparables",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ComparativeAnalysisTemplates",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "DocumentTypes",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "LandAppraisalDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "LawAndRegulations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparables",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingAnalysisMethods",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PropertyGroups",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ValuationAnalyses",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CompanyQuotations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalAssignments",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "AppraisalProperties",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "MarketComparableTemplates",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingAnalysisApproaches",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "QuotationRequests",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "Appraisals",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingAnalysis",
                schema: "appraisal");
        }
    }
}

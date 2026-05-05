using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class InitialCollateralMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "collateral");

            migrationBuilder.CreateTable(
                name: "CollateralBackfillReports",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralBackfillReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollateralMasters",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CollateralType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessage",
                schema: "collateral",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessage", x => new { x.MessageId, x.ConsumerType });
                });

            migrationBuilder.CreateTable(
                name: "CollateralEngagements",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AppraisalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AppraiserUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppraisalCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppraisalCompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralEngagements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollateralEngagements_CollateralMasters_CollateralMasterId",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollateralMasterAuditLogs",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangedFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralMasterAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollateralMasterAuditLogs_CollateralMasters_CollateralMasterId",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoDetails",
                schema: "collateral",
                columns: table => new
                {
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandOfficeCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BuildingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FloorNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TitleNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TitleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CondoName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LocationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BuildingAge = table.Column<int>(type: "int", nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastAppraisedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoDetails", x => x.CollateralMasterId);
                    table.ForeignKey(
                        name: "FK_CondoDetails_CollateralMasters_CollateralMasterId",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandDetails",
                schema: "collateral",
                columns: table => new
                {
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandOfficeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amphur = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tambon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleDeedType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TitleDeedNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SurveyOrParcelNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Village = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    LandShapeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandZoneType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrbanPlanningType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccessRoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RoadFrontage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LandArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsUnderConstructionAtLastAppraisal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OverallConstructionProgressPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    LastConstructionInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastAppraisedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LastTotalAppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandDetails", x => x.CollateralMasterId);
                    table.ForeignKey(
                        name: "FK_LandDetails_CollateralMasters_CollateralMasterId",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaseholdDetails",
                schema: "collateral",
                columns: table => new
                {
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnderlyingMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Lessor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Lessee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeaseTermStart = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaseTermEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    LeaseTermMonths = table.Column<int>(type: "int", nullable: true),
                    AnnualRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LeasePurpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastAppraisedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseholdDetails", x => x.CollateralMasterId);
                    table.ForeignKey(
                        name: "FK_LeaseholdDetails_CollateralMasters_CollateralMasterId",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaseholdDetails_UnderlyingMaster",
                        column: x => x.UnderlyingMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineDetails",
                schema: "collateral",
                columns: table => new
                {
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SerialNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EngineNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChassisNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: true),
                    MachineCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MachineAge = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LastAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastAppraisedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineDetails", x => x.CollateralMasterId);
                    table.ForeignKey(
                        name: "FK_MachineDetails_CollateralMasters_CollateralMasterId",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralBackfillReports_Status_RunAt",
                schema: "collateral",
                table: "CollateralBackfillReports",
                columns: new[] { "Status", "RunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagements_AppraisalCompanyId",
                schema: "collateral",
                table: "CollateralEngagements",
                column: "AppraisalCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagements_Master_Date",
                schema: "collateral",
                table: "CollateralEngagements",
                columns: new[] { "CollateralMasterId", "AppraisalDate" });

            migrationBuilder.CreateIndex(
                name: "UX_CollateralEngagements_AppraisalProperty",
                schema: "collateral",
                table: "CollateralEngagements",
                columns: new[] { "AppraisalId", "PropertyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasterAuditLogs_Master_ChangedAt",
                schema: "collateral",
                table: "CollateralMasterAuditLogs",
                columns: new[] { "CollateralMasterId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasters_CollateralType",
                schema: "collateral",
                table: "CollateralMasters",
                column: "CollateralType");

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasters_IsDeleted",
                schema: "collateral",
                table: "CollateralMasters",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CondoDetails_LandOffice_TitleNumber_TitleType",
                schema: "collateral",
                table: "CondoDetails",
                columns: new[] { "LandOfficeCode", "TitleNumber", "TitleType" });

            migrationBuilder.CreateIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails",
                columns: new[] { "LandOfficeCode", "CondoRegistrationNumber", "BuildingNumber", "FloorNumber", "UnitNumber", "TitleNumber", "TitleType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_Cleanup",
                schema: "collateral",
                table: "InboxMessage",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_StaleProcessing",
                schema: "collateral",
                table: "InboxMessage",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandDetails_LandOffice_TitleDeedNo",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "LandOfficeCode", "TitleDeedNo" });

            migrationBuilder.CreateIndex(
                name: "IX_LandDetails_UnderConstruction",
                schema: "collateral",
                table: "LandDetails",
                column: "IsUnderConstructionAtLastAppraisal",
                filter: "[IsUnderConstructionAtLastAppraisal] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "LandOfficeCode", "Province", "Amphur", "Tambon", "TitleDeedType", "TitleDeedNo", "SurveyOrParcelNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseholdDetails_LeaseRegistrationNo",
                schema: "collateral",
                table: "LeaseholdDetails",
                column: "LeaseRegistrationNo");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseholdDetails_UnderlyingMasterId",
                schema: "collateral",
                table: "LeaseholdDetails",
                column: "UnderlyingMasterId");

            migrationBuilder.CreateIndex(
                name: "UX_LeaseholdDetails_DedupKey_Active",
                schema: "collateral",
                table: "LeaseholdDetails",
                columns: new[] { "LeaseRegistrationNo", "UnderlyingMasterId", "Lessor", "Lessee", "LeaseTermStart" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MachineDetails_SerialNo",
                schema: "collateral",
                table: "MachineDetails",
                column: "SerialNo");

            migrationBuilder.CreateIndex(
                name: "UX_MachineDetails_Composite_Active",
                schema: "collateral",
                table: "MachineDetails",
                columns: new[] { "SerialNo", "Brand", "Model", "Manufacturer" },
                unique: true,
                filter: "[MachineRegistrationNo] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_MachineDetails_RegistrationNo_Active",
                schema: "collateral",
                table: "MachineDetails",
                column: "MachineRegistrationNo",
                unique: true,
                filter: "[MachineRegistrationNo] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollateralBackfillReports",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralEngagements",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralMasterAuditLogs",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CondoDetails",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "InboxMessage",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "LandDetails",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "LeaseholdDetails",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "MachineDetails",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralMasters",
                schema: "collateral");
        }
    }
}

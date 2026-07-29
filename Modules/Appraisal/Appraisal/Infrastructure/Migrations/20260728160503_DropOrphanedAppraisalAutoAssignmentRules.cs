using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropOrphanedAppraisalAutoAssignmentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoAssignmentRules",
                schema: "appraisal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoAssignmentRules",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AssignToCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignToTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignmentMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LoanTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaxEstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinEstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Priorities = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    PropertyTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Provinces = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoAssignmentRules", x => x.Id);
                });

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
        }
    }
}

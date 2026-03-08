using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppraisalDecisions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPriceVerified = table.Column<bool>(type: "bit", nullable: true),
                    ConditionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemarkType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppraiserOpinionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppraiserOpinion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CommitteeOpinionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CommitteeOpinion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TotalAppraisalPriceReview = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdditionalAssumptions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalDecisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalDecisions_AppraisalId",
                schema: "appraisal",
                table: "AppraisalDecisions",
                column: "AppraisalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalDecisions",
                schema: "appraisal");
        }
    }
}

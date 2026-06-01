using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReappraisalCandidateExtensionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveDateAppraisal",
                schema: "request",
                table: "ReappraisalCandidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                schema: "request",
                table: "ReappraisalCandidates",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IBGRetail",
                schema: "request",
                table: "ReappraisalCandidates",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                schema: "request",
                table: "ReappraisalCandidates",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveDateAppraisal",
                schema: "request",
                table: "ReappraisalCandidates");

            migrationBuilder.DropColumn(
                name: "Group",
                schema: "request",
                table: "ReappraisalCandidates");

            migrationBuilder.DropColumn(
                name: "IBGRetail",
                schema: "request",
                table: "ReappraisalCandidates");

            migrationBuilder.DropColumn(
                name: "Stage",
                schema: "request",
                table: "ReappraisalCandidates");
        }
    }
}

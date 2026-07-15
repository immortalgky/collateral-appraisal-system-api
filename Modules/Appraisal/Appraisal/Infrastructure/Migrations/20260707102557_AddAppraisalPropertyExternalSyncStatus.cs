using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalPropertyExternalSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalSyncError",
                schema: "appraisal",
                table: "AppraisalProperties",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSyncStatus",
                schema: "appraisal",
                table: "AppraisalProperties",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotSynced");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalSyncedAt",
                schema: "appraisal",
                table: "AppraisalProperties",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalSyncError",
                schema: "appraisal",
                table: "AppraisalProperties");

            migrationBuilder.DropColumn(
                name: "ExternalSyncStatus",
                schema: "appraisal",
                table: "AppraisalProperties");

            migrationBuilder.DropColumn(
                name: "ExternalSyncedAt",
                schema: "appraisal",
                table: "AppraisalProperties");
        }
    }
}

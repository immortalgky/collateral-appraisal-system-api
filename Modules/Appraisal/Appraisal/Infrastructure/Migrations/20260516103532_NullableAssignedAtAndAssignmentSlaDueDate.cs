using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NullableAssignedAtAndAssignmentSlaDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "AssignedAt",
                schema: "appraisal",
                table: "AppraisalAssignments",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "SLADueDate",
                schema: "appraisal",
                table: "AppraisalAssignments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SLADueDate",
                schema: "appraisal",
                table: "AppraisalAssignments");

            // SQL Server ALTER COLUMN to non-nullable fails when existing rows contain NULL.
            // Back-fill NULLs with the EF default before the column type change.
            migrationBuilder.Sql(
                "UPDATE [appraisal].[AppraisalAssignments] SET AssignedAt = '0001-01-01' WHERE AssignedAt IS NULL");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AssignedAt",
                schema: "appraisal",
                table: "AppraisalAssignments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}

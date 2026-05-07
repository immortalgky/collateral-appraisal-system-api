using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrevAppraisalIdToAppraisal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrevAppraisalId",
                schema: "appraisal",
                table: "Appraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_PrevAppraisalId",
                schema: "appraisal",
                table: "Appraisals",
                column: "PrevAppraisalId",
                filter: "[PrevAppraisalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appraisals_PrevAppraisalId",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "PrevAppraisalId",
                schema: "appraisal",
                table: "Appraisals");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxProcessingStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                schema: "collateral",
                table: "IntegrationEventOutbox",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                schema: "collateral",
                table: "IntegrationEventOutbox");
        }
    }
}

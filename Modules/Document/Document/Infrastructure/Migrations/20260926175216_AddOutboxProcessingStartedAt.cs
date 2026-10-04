using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxProcessingStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                schema: "document",
                table: "IntegrationEventOutbox",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                schema: "document",
                table: "IntegrationEventOutbox");
        }
    }
}

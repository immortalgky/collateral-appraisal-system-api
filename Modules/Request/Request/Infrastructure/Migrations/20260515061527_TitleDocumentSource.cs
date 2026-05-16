using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TitleDocumentSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "request",
                table: "RequestTitleDocuments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            // Backfill all existing rows to 'REQUEST' — they were all created before the
            // document-followup flow existed, so REQUEST is the correct source for every one.
            migrationBuilder.Sql(
                "UPDATE [request].[RequestTitleDocuments] SET [Source] = 'REQUEST' WHERE [Source] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                schema: "request",
                table: "RequestTitleDocuments");
        }
    }
}

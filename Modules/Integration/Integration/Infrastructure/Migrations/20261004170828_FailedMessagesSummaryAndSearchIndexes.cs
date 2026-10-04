using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FailedMessagesSummaryAndSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_ActionAt",
                schema: "integration",
                table: "FailedMessages",
                columns: new[] { "Status", "ActionAt" });

            // Widen IX_FailedMessages_Status_FaultedAt in place. DROP_EXISTING rebuilds it in one statement, so there
            // is never a moment without the index (a DropIndex + CreateIndex pair leaves the 15 s summary poll
            // scanning the ~10 KB-wide rows in between). Not ONLINE = ON: the edition may not support it, so the
            // rebuild is offline and blocks writes to FailedMessages while it runs.
            migrationBuilder.Sql(
                @"CREATE NONCLUSTERED INDEX [IX_FailedMessages_Status_FaultedAt]
    ON [integration].[FailedMessages] ([Status], [FaultedAt])
    INCLUDE ([SourceQueue], [ExceptionType], [Node], [Kind], [MessageId], [RefNumber], [ExceptionMessage])
    WITH (DROP_EXISTING = ON);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_Status_ActionAt",
                schema: "integration",
                table: "FailedMessages");

            // Back to the plain (Status, FaultedAt) index, again in place.
            migrationBuilder.Sql(
                @"CREATE NONCLUSTERED INDEX [IX_FailedMessages_Status_FaultedAt]
    ON [integration].[FailedMessages] ([Status], [FaultedAt])
    WITH (DROP_EXISTING = ON);");
        }
    }
}

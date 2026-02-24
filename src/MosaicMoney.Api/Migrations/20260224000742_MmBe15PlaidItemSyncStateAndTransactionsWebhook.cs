using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmBe15PlaidItemSyncStateAndTransactionsWebhook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaidItemSyncStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlaidEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InitialUpdateComplete = table.Column<bool>(type: "boolean", nullable: false),
                    HistoricalUpdateComplete = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastWebhookAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastSyncErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastSyncErrorAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncStatus = table.Column<int>(type: "integer", nullable: false),
                    PendingWebhookCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidItemSyncStates", x => x.Id);
                    table.CheckConstraint("CK_PlaidItemSyncState_CursorRequired", "LENGTH(TRIM(\"Cursor\")) > 0");
                    table.CheckConstraint("CK_PlaidItemSyncState_ItemIdRequired", "LENGTH(TRIM(\"ItemId\")) > 0");
                    table.CheckConstraint("CK_PlaidItemSyncState_LastSyncErrorAudit", "(\"LastSyncErrorCode\" IS NULL AND \"LastSyncErrorAtUtc\" IS NULL) OR (\"LastSyncErrorCode\" IS NOT NULL AND LENGTH(TRIM(\"LastSyncErrorCode\")) > 0 AND \"LastSyncErrorAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_PlaidItemSyncState_PendingWebhookCountRange", "\"PendingWebhookCount\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItemSyncStates_PlaidEnvironment_ItemId",
                table: "PlaidItemSyncStates",
                columns: new[] { "PlaidEnvironment", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItemSyncStates_SyncStatus_LastWebhookAtUtc_LastSyncedA~",
                table: "PlaidItemSyncStates",
                columns: new[] { "SyncStatus", "LastWebhookAtUtc", "LastSyncedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaidItemSyncStates");
        }
    }
}

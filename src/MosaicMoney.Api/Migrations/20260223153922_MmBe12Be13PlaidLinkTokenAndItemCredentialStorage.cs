using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmBe12Be13PlaidLinkTokenAndItemCredentialStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaidItemCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlaidEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccessTokenCiphertext = table.Column<string>(type: "text", nullable: false),
                    AccessTokenFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRotatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastLinkedSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastClientMetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidItemCredentials", x => x.Id);
                    table.CheckConstraint("CK_PlaidItemCredential_AccessTokenCiphertextRequired", "LENGTH(TRIM(\"AccessTokenCiphertext\")) > 0");
                    table.CheckConstraint("CK_PlaidItemCredential_AccessTokenFingerprintRequired", "LENGTH(TRIM(\"AccessTokenFingerprint\")) > 0");
                    table.CheckConstraint("CK_PlaidItemCredential_ItemIdRequired", "LENGTH(TRIM(\"ItemId\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "PlaidLinkSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LinkTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OAuthStateId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedProducts = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LinkTokenCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkTokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEventAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastClientMetadataJson = table.Column<string>(type: "text", nullable: true),
                    LinkedItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidLinkSessions", x => x.Id);
                    table.CheckConstraint("CK_PlaidLinkSession_ClientUserIdRequired", "LENGTH(TRIM(\"ClientUserId\")) > 0");
                    table.CheckConstraint("CK_PlaidLinkSession_LinkTokenHashRequired", "LENGTH(TRIM(\"LinkTokenHash\")) > 0");
                    table.CheckConstraint("CK_PlaidLinkSession_RequestedProductsRequired", "LENGTH(TRIM(\"RequestedProducts\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "PlaidLinkSessionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaidLinkSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClientMetadataJson = table.Column<string>(type: "text", nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidLinkSessionEvents", x => x.Id);
                    table.CheckConstraint("CK_PlaidLinkSessionEvent_EventTypeRequired", "LENGTH(TRIM(\"EventType\")) > 0");
                    table.ForeignKey(
                        name: "FK_PlaidLinkSessionEvents_PlaidLinkSessions_PlaidLinkSessionId",
                        column: x => x.PlaidLinkSessionId,
                        principalTable: "PlaidLinkSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItemCredentials_HouseholdId_Status_LastRotatedAtUtc",
                table: "PlaidItemCredentials",
                columns: new[] { "HouseholdId", "Status", "LastRotatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItemCredentials_PlaidEnvironment_ItemId",
                table: "PlaidItemCredentials",
                columns: new[] { "PlaidEnvironment", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaidLinkSessionEvents_PlaidLinkSessionId_OccurredAtUtc",
                table: "PlaidLinkSessionEvents",
                columns: new[] { "PlaidLinkSessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidLinkSessions_HouseholdId_LinkTokenCreatedAtUtc",
                table: "PlaidLinkSessions",
                columns: new[] { "HouseholdId", "LinkTokenCreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidLinkSessions_LinkTokenHash",
                table: "PlaidLinkSessions",
                column: "LinkTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaidItemCredentials");

            migrationBuilder.DropTable(
                name: "PlaidLinkSessionEvents");

            migrationBuilder.DropTable(
                name: "PlaidLinkSessions");
        }
    }
}

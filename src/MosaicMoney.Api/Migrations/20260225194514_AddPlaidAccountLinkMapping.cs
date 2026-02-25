using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaidAccountLinkMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaidAccountLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaidItemCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlaidEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlaidAccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnlinkedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidAccountLinks", x => x.Id);
                    table.CheckConstraint("CK_PlaidAccountLink_ItemIdRequired", "LENGTH(TRIM(\"ItemId\")) > 0");
                    table.CheckConstraint("CK_PlaidAccountLink_LastSeenAfterLinked", "\"LastSeenAtUtc\" >= \"LinkedAtUtc\"");
                    table.CheckConstraint("CK_PlaidAccountLink_PlaidAccountIdRequired", "LENGTH(TRIM(\"PlaidAccountId\")) > 0");
                    table.CheckConstraint("CK_PlaidAccountLink_UnlinkAudit", "(\"IsActive\" = TRUE AND \"UnlinkedAtUtc\" IS NULL) OR (\"IsActive\" = FALSE AND \"UnlinkedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PlaidAccountLinks_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaidAccountLinks_PlaidItemCredentials_PlaidItemCredentialId",
                        column: x => x.PlaidItemCredentialId,
                        principalTable: "PlaidItemCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidAccountLinks_AccountId",
                table: "PlaidAccountLinks",
                column: "AccountId",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PlaidAccountLinks_PlaidEnvironment_ItemId_PlaidAccountId",
                table: "PlaidAccountLinks",
                columns: new[] { "PlaidEnvironment", "ItemId", "PlaidAccountId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PlaidAccountLinks_PlaidItemCredentialId_IsActive_LastSeenAt~",
                table: "PlaidAccountLinks",
                columns: new[] { "PlaidItemCredentialId", "IsActive", "LastSeenAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaidAccountLinks");
        }
    }
}

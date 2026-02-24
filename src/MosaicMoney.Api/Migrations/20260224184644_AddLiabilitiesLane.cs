using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLiabilitiesLane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiabilityAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlaidEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlaidAccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OfficialName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mask = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AccountType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AccountSubtype = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiabilityAccounts", x => x.Id);
                    table.CheckConstraint("CK_LiabilityAccount_ItemIdRequired", "LENGTH(TRIM(\"ItemId\")) > 0");
                    table.CheckConstraint("CK_LiabilityAccount_NameRequired", "LENGTH(TRIM(\"Name\")) > 0");
                    table.CheckConstraint("CK_LiabilityAccount_PlaidAccountIdRequired", "LENGTH(TRIM(\"PlaidAccountId\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "LiabilitySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LiabilityAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LiabilityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LastStatementBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimumPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LastPaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextPaymentDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Apr = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawLiabilityJson = table.Column<string>(type: "text", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiabilitySnapshots", x => x.Id);
                    table.CheckConstraint("CK_LiabilitySnapshot_AprRange", "\"Apr\" IS NULL OR (\"Apr\" >= 0 AND \"Apr\" <= 100)");
                    table.CheckConstraint("CK_LiabilitySnapshot_LiabilityTypeRequired", "LENGTH(TRIM(\"LiabilityType\")) > 0");
                    table.CheckConstraint("CK_LiabilitySnapshot_SnapshotHashRequired", "LENGTH(TRIM(\"SnapshotHash\")) > 0");
                    table.ForeignKey(
                        name: "FK_LiabilitySnapshots_LiabilityAccounts_LiabilityAccountId",
                        column: x => x.LiabilityAccountId,
                        principalTable: "LiabilityAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiabilityAccounts_HouseholdId_IsActive_LastSeenAtUtc",
                table: "LiabilityAccounts",
                columns: new[] { "HouseholdId", "IsActive", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LiabilityAccounts_PlaidEnvironment_ItemId_PlaidAccountId",
                table: "LiabilityAccounts",
                columns: new[] { "PlaidEnvironment", "ItemId", "PlaidAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiabilitySnapshots_LiabilityAccountId_CapturedAtUtc",
                table: "LiabilitySnapshots",
                columns: new[] { "LiabilityAccountId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LiabilitySnapshots_LiabilityAccountId_SnapshotHash",
                table: "LiabilitySnapshots",
                columns: new[] { "LiabilityAccountId", "SnapshotHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiabilitySnapshots");

            migrationBuilder.DropTable(
                name: "LiabilityAccounts");
        }
    }
}

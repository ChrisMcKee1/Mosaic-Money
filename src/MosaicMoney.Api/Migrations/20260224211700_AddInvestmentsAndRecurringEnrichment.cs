using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentsAndRecurringEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaidRecurringConfidence",
                table: "RecurringItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlaidRecurringLastSeenAtUtc",
                table: "RecurringItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaidRecurringStreamId",
                table: "RecurringItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurringSource",
                table: "RecurringItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InvestmentAccounts",
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
                    table.PrimaryKey("PK_InvestmentAccounts", x => x.Id);
                    table.CheckConstraint("CK_InvestmentAccount_ItemIdRequired", "LENGTH(TRIM(\"ItemId\")) > 0");
                    table.CheckConstraint("CK_InvestmentAccount_NameRequired", "LENGTH(TRIM(\"Name\")) > 0");
                    table.CheckConstraint("CK_InvestmentAccount_PlaidAccountIdRequired", "LENGTH(TRIM(\"PlaidAccountId\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "InvestmentHoldingSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaidSecurityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TickerSymbol = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InstitutionPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InstitutionPriceAsOf = table.Column<DateOnly>(type: "date", nullable: true),
                    InstitutionValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostBasis = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawHoldingJson = table.Column<string>(type: "text", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentHoldingSnapshots", x => x.Id);
                    table.CheckConstraint("CK_InvestmentHoldingSnapshot_PlaidSecurityIdRequired", "LENGTH(TRIM(\"PlaidSecurityId\")) > 0");
                    table.CheckConstraint("CK_InvestmentHoldingSnapshot_SnapshotHashRequired", "LENGTH(TRIM(\"SnapshotHash\")) > 0");
                    table.ForeignKey(
                        name: "FK_InvestmentHoldingSnapshots_InvestmentAccounts_InvestmentAcc~",
                        column: x => x.InvestmentAccountId,
                        principalTable: "InvestmentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaidInvestmentTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlaidSecurityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Fees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Subtype = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawTransactionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentTransactions", x => x.Id);
                    table.CheckConstraint("CK_InvestmentTransaction_NameRequired", "LENGTH(TRIM(\"Name\")) > 0");
                    table.CheckConstraint("CK_InvestmentTransaction_PlaidInvestmentTransactionIdRequired", "LENGTH(TRIM(\"PlaidInvestmentTransactionId\")) > 0");
                    table.ForeignKey(
                        name: "FK_InvestmentTransactions_InvestmentAccounts_InvestmentAccount~",
                        column: x => x.InvestmentAccountId,
                        principalTable: "InvestmentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccounts_HouseholdId_IsActive_LastSeenAtUtc",
                table: "InvestmentAccounts",
                columns: new[] { "HouseholdId", "IsActive", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccounts_PlaidEnvironment_ItemId_PlaidAccountId",
                table: "InvestmentAccounts",
                columns: new[] { "PlaidEnvironment", "ItemId", "PlaidAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentHoldingSnapshots_InvestmentAccountId_CapturedAtUtc",
                table: "InvestmentHoldingSnapshots",
                columns: new[] { "InvestmentAccountId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentHoldingSnapshots_InvestmentAccountId_SnapshotHash",
                table: "InvestmentHoldingSnapshots",
                columns: new[] { "InvestmentAccountId", "SnapshotHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_InvestmentAccountId_Date",
                table: "InvestmentTransactions",
                columns: new[] { "InvestmentAccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_InvestmentAccountId_PlaidInvestmentT~",
                table: "InvestmentTransactions",
                columns: new[] { "InvestmentAccountId", "PlaidInvestmentTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentHoldingSnapshots");

            migrationBuilder.DropTable(
                name: "InvestmentTransactions");

            migrationBuilder.DropTable(
                name: "InvestmentAccounts");

            migrationBuilder.DropColumn(
                name: "PlaidRecurringConfidence",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "PlaidRecurringLastSeenAtUtc",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "PlaidRecurringStreamId",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "RecurringSource",
                table: "RecurringItems");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountAccessPolicyBackfillReviewQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AccessPolicyNeedsReview",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountAccessPolicyReviewQueueEntries",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EnqueuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountAccessPolicyReviewQueueEntries", x => x.AccountId);
                    table.CheckConstraint("CK_AccountAccessPolicyReviewQueueEntry_EvaluationAfterEnqueue", "\"LastEvaluatedAtUtc\" >= \"EnqueuedAtUtc\"");
                    table.CheckConstraint("CK_AccountAccessPolicyReviewQueueEntry_RationaleRequired", "LENGTH(TRIM(\"Rationale\")) > 0");
                    table.CheckConstraint("CK_AccountAccessPolicyReviewQueueEntry_ReasonCodeRequired", "LENGTH(TRIM(\"ReasonCode\")) > 0");
                    table.CheckConstraint("CK_AccountAccessPolicyReviewQueueEntry_ResolutionAfterEnqueue", "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"EnqueuedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_AccountAccessPolicyReviewQueueEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountAccessPolicyReviewQueueEntries_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountAccessPolicyReviewQueueEntries_HouseholdId_ResolvedA~",
                table: "AccountAccessPolicyReviewQueueEntries",
                columns: new[] { "HouseholdId", "ResolvedAtUtc", "LastEvaluatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountAccessPolicyReviewQueueEntries");

            migrationBuilder.DropColumn(
                name: "AccessPolicyNeedsReview",
                table: "Accounts");
        }
    }
}

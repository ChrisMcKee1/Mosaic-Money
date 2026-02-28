using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClassificationInsightAndAssignmentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedByAgent",
                table: "TransactionClassificationOutcomes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentSource",
                table: "TransactionClassificationOutcomes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "human");

            migrationBuilder.AddColumn<bool>(
                name: "IsAiAssigned",
                table: "TransactionClassificationOutcomes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ClassificationInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    InsightType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationInsights", x => x.Id);
                    table.CheckConstraint("CK_ClassificationInsight_ConfidenceRange", "\"Confidence\" >= 0 AND \"Confidence\" <= 1");
                    table.CheckConstraint("CK_ClassificationInsight_InsightTypeRequired", "LENGTH(TRIM(\"InsightType\")) > 0");
                    table.CheckConstraint("CK_ClassificationInsight_SummaryRequired", "LENGTH(TRIM(\"Summary\")) > 0");
                    table.ForeignKey(
                        name: "FK_ClassificationInsights_EnrichedTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassificationInsights_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassificationInsights_TransactionClassificationOutcomes_Ou~",
                        column: x => x.OutcomeId,
                        principalTable: "TransactionClassificationOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionClassificationOutcome_AssignmentSourceRequired",
                table: "TransactionClassificationOutcomes",
                sql: "LENGTH(TRIM(\"AssignmentSource\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationInsights_HouseholdId_CreatedAtUtc",
                table: "ClassificationInsights",
                columns: new[] { "HouseholdId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationInsights_OutcomeId_CreatedAtUtc",
                table: "ClassificationInsights",
                columns: new[] { "OutcomeId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationInsights_TransactionId",
                table: "ClassificationInsights",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassificationInsights");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionClassificationOutcome_AssignmentSourceRequired",
                table: "TransactionClassificationOutcomes");

            migrationBuilder.DropColumn(
                name: "AssignedByAgent",
                table: "TransactionClassificationOutcomes");

            migrationBuilder.DropColumn(
                name: "AssignmentSource",
                table: "TransactionClassificationOutcomes");

            migrationBuilder.DropColumn(
                name: "IsAiAssigned",
                table: "TransactionClassificationOutcomes");
        }
    }
}

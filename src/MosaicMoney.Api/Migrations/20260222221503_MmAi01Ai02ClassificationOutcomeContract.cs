using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmAi01Ai02ClassificationOutcomeContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionClassificationOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedSubcategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false),
                    DecisionReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DecisionRationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AgentNoteSummary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionClassificationOutcomes", x => x.Id);
                    table.CheckConstraint("CK_TransactionClassificationOutcome_DecisionReviewRouting", "(\"Decision\" = 2 AND \"ReviewStatus\" = 1) OR (\"Decision\" = 1 AND \"ReviewStatus\" <> 1)");
                    table.CheckConstraint("CK_TransactionClassificationOutcome_FinalConfidenceRange", "\"FinalConfidence\" >= 0 AND \"FinalConfidence\" <= 1");
                    table.ForeignKey(
                        name: "FK_TransactionClassificationOutcomes_EnrichedTransactions_Tran~",
                        column: x => x.TransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionClassificationOutcomes_Subcategories_ProposedSub~",
                        column: x => x.ProposedSubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClassificationStageOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    StageOrder = table.Column<int>(type: "integer", nullable: false),
                    ProposedSubcategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    RationaleCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EscalatedToNextStage = table.Column<bool>(type: "boolean", nullable: false),
                    ProducedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationStageOutputs", x => x.Id);
                    table.CheckConstraint("CK_ClassificationStageOutput_ConfidenceRange", "\"Confidence\" >= 0 AND \"Confidence\" <= 1");
                    table.CheckConstraint("CK_ClassificationStageOutput_StageOrderRange", "\"StageOrder\" >= 1 AND \"StageOrder\" <= 3");
                    table.ForeignKey(
                        name: "FK_ClassificationStageOutputs_Subcategories_ProposedSubcategor~",
                        column: x => x.ProposedSubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassificationStageOutputs_TransactionClassificationOutcome~",
                        column: x => x.OutcomeId,
                        principalTable: "TransactionClassificationOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationStageOutputs_OutcomeId_Stage",
                table: "ClassificationStageOutputs",
                columns: new[] { "OutcomeId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationStageOutputs_ProposedSubcategoryId",
                table: "ClassificationStageOutputs",
                column: "ProposedSubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationOutcomes_Decision_ReviewStatus_Cre~",
                table: "TransactionClassificationOutcomes",
                columns: new[] { "Decision", "ReviewStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationOutcomes_ProposedSubcategoryId",
                table: "TransactionClassificationOutcomes",
                column: "ProposedSubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationOutcomes_TransactionId_CreatedAtUtc",
                table: "TransactionClassificationOutcomes",
                columns: new[] { "TransactionId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassificationStageOutputs");

            migrationBuilder.DropTable(
                name: "TransactionClassificationOutcomes");
        }
    }
}

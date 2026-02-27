using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWorkflowLifecycleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WorkflowName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TriggerSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FailureRationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.CheckConstraint("CK_AgentRun_CompletedAfterCreated", "\"CompletedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_AgentRun_CorrelationIdRequired", "LENGTH(TRIM(\"CorrelationId\")) > 0");
                    table.CheckConstraint("CK_AgentRun_FailureAuditForEscalatedStates", "((\"Status\" IN (4, 5, 6)) AND \"FailureCode\" IS NOT NULL AND LENGTH(TRIM(\"FailureCode\")) > 0 AND \"FailureRationale\" IS NOT NULL AND LENGTH(TRIM(\"FailureRationale\")) > 0) OR (\"Status\" NOT IN (4, 5, 6))");
                    table.CheckConstraint("CK_AgentRun_LastModifiedAfterCreated", "\"LastModifiedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_AgentRun_PolicyVersionRequired", "LENGTH(TRIM(\"PolicyVersion\")) > 0");
                    table.CheckConstraint("CK_AgentRun_StatusRange", "\"Status\" IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_AgentRun_TerminalCompletionAudit", "((\"Status\" IN (1, 2)) AND \"CompletedAtUtc\" IS NULL) OR ((\"Status\" IN (3, 4, 5, 6)) AND \"CompletedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_AgentRun_TriggerSourceRequired", "LENGTH(TRIM(\"TriggerSource\")) > 0");
                    table.CheckConstraint("CK_AgentRun_WorkflowNameRequired", "LENGTH(TRIM(\"WorkflowName\")) > 0");
                    table.ForeignKey(
                        name: "FK_AgentRuns_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentRunStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StageOrder = table.Column<int>(type: "integer", nullable: false),
                    Executor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    OutcomeCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OutcomeRationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AgentNoteSummary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRunStages", x => x.Id);
                    table.CheckConstraint("CK_AgentRunStage_CompletedAfterCreated", "\"CompletedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_AgentRunStage_ConfidenceRange", "\"Confidence\" IS NULL OR (\"Confidence\" >= 0 AND \"Confidence\" <= 1)");
                    table.CheckConstraint("CK_AgentRunStage_ExecutorRequired", "LENGTH(TRIM(\"Executor\")) > 0");
                    table.CheckConstraint("CK_AgentRunStage_LastModifiedAfterCreated", "\"LastModifiedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_AgentRunStage_StageNameRequired", "LENGTH(TRIM(\"StageName\")) > 0");
                    table.CheckConstraint("CK_AgentRunStage_StageOrderRange", "\"StageOrder\" >= 1 AND \"StageOrder\" <= 64");
                    table.CheckConstraint("CK_AgentRunStage_StatusRange", "\"Status\" IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_AgentRunStage_TerminalCompletionAudit", "((\"Status\" IN (1, 2)) AND \"CompletedAtUtc\" IS NULL) OR ((\"Status\" IN (3, 4, 5, 6)) AND \"CompletedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_AgentRunStage_TerminalOutcomeRequired", "(\"Status\" IN (1, 2)) OR (\"OutcomeCode\" IS NOT NULL AND LENGTH(TRIM(\"OutcomeCode\")) > 0 AND \"OutcomeRationale\" IS NOT NULL AND LENGTH(TRIM(\"OutcomeRationale\")) > 0)");
                    table.ForeignKey(
                        name: "FK_AgentRunStages_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    KeyValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ResolutionRationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                    table.CheckConstraint("CK_IdempotencyKey_ExpiresAfterCreated", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_IdempotencyKey_FinalizationAudit", "((\"Status\" = 1) AND \"FinalizedAtUtc\" IS NULL) OR ((\"Status\" IN (2, 3, 4)) AND \"FinalizedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_IdempotencyKey_FinalizedAfterCreated", "\"FinalizedAtUtc\" IS NULL OR \"FinalizedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_IdempotencyKey_KeyValueRequired", "LENGTH(TRIM(\"KeyValue\")) > 0");
                    table.CheckConstraint("CK_IdempotencyKey_RequestHashRequired", "LENGTH(TRIM(\"RequestHash\")) > 0");
                    table.CheckConstraint("CK_IdempotencyKey_ResolutionAudit", "(\"ResolutionCode\" IS NULL AND \"ResolutionRationale\" IS NULL) OR (\"ResolutionCode\" IS NOT NULL AND LENGTH(TRIM(\"ResolutionCode\")) > 0 AND \"ResolutionRationale\" IS NOT NULL AND LENGTH(TRIM(\"ResolutionRationale\")) > 0)");
                    table.CheckConstraint("CK_IdempotencyKey_ScopeRequired", "LENGTH(TRIM(\"Scope\")) > 0");
                    table.CheckConstraint("CK_IdempotencyKey_StatusRange", "\"Status\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentDecisionAudit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false),
                    DecisionType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    AgentNoteSummary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDecisionAudit", x => x.Id);
                    table.CheckConstraint("CK_AgentDecisionAudit_ConfidenceRange", "\"Confidence\" IS NULL OR (\"Confidence\" >= 0 AND \"Confidence\" <= 1)");
                    table.CheckConstraint("CK_AgentDecisionAudit_DecisionTypeRequired", "LENGTH(TRIM(\"DecisionType\")) > 0");
                    table.CheckConstraint("CK_AgentDecisionAudit_FailClosedNeedsReview", "(\"Outcome\" = 2 AND \"ReviewStatus\" = 1) OR (\"Outcome\" <> 2 AND \"ReviewStatus\" <> 1)");
                    table.CheckConstraint("CK_AgentDecisionAudit_OutcomeRange", "\"Outcome\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_AgentDecisionAudit_PolicyVersionRequired", "LENGTH(TRIM(\"PolicyVersion\")) > 0");
                    table.CheckConstraint("CK_AgentDecisionAudit_RationaleRequired", "LENGTH(TRIM(\"Rationale\")) > 0");
                    table.CheckConstraint("CK_AgentDecisionAudit_ReasonCodeRequired", "LENGTH(TRIM(\"ReasonCode\")) > 0");
                    table.CheckConstraint("CK_AgentDecisionAudit_ReviewedAfterDecision", "\"ReviewedAtUtc\" IS NULL OR \"ReviewedAtUtc\" >= \"DecidedAtUtc\"");
                    table.CheckConstraint("CK_AgentDecisionAudit_ReviewedAudit", "(\"ReviewedByUserId\" IS NULL AND \"ReviewedAtUtc\" IS NULL) OR (\"ReviewedByUserId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AgentDecisionAudit_AgentRunStages_AgentRunStageId",
                        column: x => x.AgentRunStageId,
                        principalTable: "AgentRunStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentDecisionAudit_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentDecisionAudit_HouseholdUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "HouseholdUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignalCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    RaisedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSignals", x => x.Id);
                    table.CheckConstraint("CK_AgentSignal_HumanReviewRequiredForHighSeverity", "(\"Severity\" IN (1, 2)) OR \"RequiresHumanReview\" = TRUE");
                    table.CheckConstraint("CK_AgentSignal_ResolutionAudit", "(\"IsResolved\" = TRUE AND \"ResolvedAtUtc\" IS NOT NULL) OR (\"IsResolved\" = FALSE AND \"ResolvedAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_AgentSignal_SeverityRange", "\"Severity\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_AgentSignal_SignalCodeRequired", "LENGTH(TRIM(\"SignalCode\")) > 0");
                    table.CheckConstraint("CK_AgentSignal_SummaryRequired", "LENGTH(TRIM(\"Summary\")) > 0");
                    table.ForeignKey(
                        name: "FK_AgentSignals_AgentRunStages_AgentRunStageId",
                        column: x => x.AgentRunStageId,
                        principalTable: "AgentRunStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentSignals_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDecisionAudit_AgentRunId_DecidedAtUtc",
                table: "AgentDecisionAudit",
                columns: new[] { "AgentRunId", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDecisionAudit_AgentRunStageId_DecidedAtUtc",
                table: "AgentDecisionAudit",
                columns: new[] { "AgentRunStageId", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDecisionAudit_Outcome_ReviewStatus_DecidedAtUtc",
                table: "AgentDecisionAudit",
                columns: new[] { "Outcome", "ReviewStatus", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDecisionAudit_ReviewedByUserId_ReviewedAtUtc",
                table: "AgentDecisionAudit",
                columns: new[] { "ReviewedByUserId", "ReviewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_CorrelationId_CreatedAtUtc",
                table: "AgentRuns",
                columns: new[] { "CorrelationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_HouseholdId_Status_CreatedAtUtc",
                table: "AgentRuns",
                columns: new[] { "HouseholdId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_WorkflowName_TriggerSource_CreatedAtUtc",
                table: "AgentRuns",
                columns: new[] { "WorkflowName", "TriggerSource", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRunStages_AgentRunId_StageOrder",
                table: "AgentRunStages",
                columns: new[] { "AgentRunId", "StageOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRunStages_AgentRunId_Status_StageOrder",
                table: "AgentRunStages",
                columns: new[] { "AgentRunId", "Status", "StageOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRunStages_Status_CreatedAtUtc",
                table: "AgentRunStages",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSignals_AgentRunId_RaisedAtUtc",
                table: "AgentSignals",
                columns: new[] { "AgentRunId", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSignals_AgentRunStageId_RaisedAtUtc",
                table: "AgentSignals",
                columns: new[] { "AgentRunStageId", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSignals_RequiresHumanReview_IsResolved_RaisedAtUtc",
                table: "AgentSignals",
                columns: new[] { "RequiresHumanReview", "IsResolved", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_AgentRunId_CreatedAtUtc",
                table: "IdempotencyKeys",
                columns: new[] { "AgentRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_Scope_KeyValue",
                table: "IdempotencyKeys",
                columns: new[] { "Scope", "KeyValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_Status_ExpiresAtUtc",
                table: "IdempotencyKeys",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDecisionAudit");

            migrationBuilder.DropTable(
                name: "AgentSignals");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropTable(
                name: "AgentRunStages");

            migrationBuilder.DropTable(
                name: "AgentRuns");
        }
    }
}

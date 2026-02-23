using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmBe07A08ARecurringAndReimbursementContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleGroupId",
                table: "ReimbursementProposals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "LifecycleOrdinal",
                table: "ReimbursementProposals",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ProposalSource",
                table: "ReimbursementProposals",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ProvenancePayloadJson",
                table: "ReimbursementProposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvenanceReference",
                table: "ReimbursementProposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvenanceSource",
                table: "ReimbursementProposals",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "StatusRationale",
                table: "ReimbursementProposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Proposal created and awaiting human review.");

            migrationBuilder.AddColumn<string>(
                name: "StatusReasonCode",
                table: "ReimbursementProposals",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "proposal_created");

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesProposalId",
                table: "ReimbursementProposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountScoreWeight",
                table: "RecurringItems",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.3500m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountVarianceAbsolute",
                table: "RecurringItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountVariancePercent",
                table: "RecurringItems",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 5.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeterministicMatchThreshold",
                table: "RecurringItems",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.7000m);

            migrationBuilder.AddColumn<string>(
                name: "DeterministicScoreVersion",
                table: "RecurringItems",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "mm-be-07a-v1");

            migrationBuilder.AddColumn<decimal>(
                name: "DueDateScoreWeight",
                table: "RecurringItems",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.5000m);

            migrationBuilder.AddColumn<int>(
                name: "DueWindowDaysAfter",
                table: "RecurringItems",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "DueWindowDaysBefore",
                table: "RecurringItems",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "RecencyScoreWeight",
                table: "RecurringItems",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.1500m);

            migrationBuilder.AddColumn<string>(
                name: "TieBreakPolicy",
                table: "RecurringItems",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "due_date_distance_then_amount_delta_then_latest_observed");

            migrationBuilder.Sql("""
                UPDATE "ReimbursementProposals"
                SET "LifecycleGroupId" = "Id",
                    "LifecycleOrdinal" = 1,
                    "ProposalSource" = 1,
                    "ProvenanceSource" = 'unknown',
                    "StatusReasonCode" = 'proposal_created',
                    "StatusRationale" = 'Proposal created and awaiting human review.'
                WHERE "LifecycleGroupId" = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_IncomingTransactionId_LifecycleGroup~",
                table: "ReimbursementProposals",
                columns: new[] { "IncomingTransactionId", "LifecycleGroupId", "LifecycleOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_LifecycleGroupId_CreatedAtUtc",
                table: "ReimbursementProposals",
                columns: new[] { "LifecycleGroupId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_SupersedesProposalId",
                table: "ReimbursementProposals",
                column: "SupersedesProposalId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReimbursementProposal_DecisionAuditForFinalStates",
                table: "ReimbursementProposals",
                sql: "(\"Status\" IN (2, 3) AND \"DecisionedByUserId\" IS NOT NULL AND \"DecisionedAtUtc\" IS NOT NULL) OR (\"Status\" NOT IN (2, 3))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReimbursementProposal_LifecycleOrdinal",
                table: "ReimbursementProposals",
                sql: "\"LifecycleOrdinal\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReimbursementProposal_ProvenanceRequired",
                table: "ReimbursementProposals",
                sql: "LENGTH(TRIM(\"ProvenanceSource\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReimbursementProposal_RationaleRequired",
                table: "ReimbursementProposals",
                sql: "LENGTH(TRIM(\"StatusReasonCode\")) > 0 AND LENGTH(TRIM(\"StatusRationale\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringItem_DeterministicMetadataRequired",
                table: "RecurringItems",
                sql: "LENGTH(TRIM(\"DeterministicScoreVersion\")) > 0 AND LENGTH(TRIM(\"TieBreakPolicy\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringItem_DeterministicThresholdRange",
                table: "RecurringItems",
                sql: "\"DeterministicMatchThreshold\" >= 0 AND \"DeterministicMatchThreshold\" <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringItem_DueWindowRange",
                table: "RecurringItems",
                sql: "\"DueWindowDaysBefore\" >= 0 AND \"DueWindowDaysBefore\" <= 90 AND \"DueWindowDaysAfter\" >= 0 AND \"DueWindowDaysAfter\" <= 90");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringItem_ScoreWeightsRange",
                table: "RecurringItems",
                sql: "\"DueDateScoreWeight\" >= 0 AND \"DueDateScoreWeight\" <= 1 AND \"AmountScoreWeight\" >= 0 AND \"AmountScoreWeight\" <= 1 AND \"RecencyScoreWeight\" >= 0 AND \"RecencyScoreWeight\" <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringItem_ScoreWeightsSum",
                table: "RecurringItems",
                sql: "ROUND(\"DueDateScoreWeight\" + \"AmountScoreWeight\" + \"RecencyScoreWeight\", 4) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringItem_VarianceRange",
                table: "RecurringItems",
                sql: "\"AmountVariancePercent\" >= 0 AND \"AmountVariancePercent\" <= 100 AND \"AmountVarianceAbsolute\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_ReimbursementProposals_ReimbursementProposals_SupersedesPro~",
                table: "ReimbursementProposals",
                column: "SupersedesProposalId",
                principalTable: "ReimbursementProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReimbursementProposals_ReimbursementProposals_SupersedesPro~",
                table: "ReimbursementProposals");

            migrationBuilder.DropIndex(
                name: "IX_ReimbursementProposals_IncomingTransactionId_LifecycleGroup~",
                table: "ReimbursementProposals");

            migrationBuilder.DropIndex(
                name: "IX_ReimbursementProposals_LifecycleGroupId_CreatedAtUtc",
                table: "ReimbursementProposals");

            migrationBuilder.DropIndex(
                name: "IX_ReimbursementProposals_SupersedesProposalId",
                table: "ReimbursementProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReimbursementProposal_DecisionAuditForFinalStates",
                table: "ReimbursementProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReimbursementProposal_LifecycleOrdinal",
                table: "ReimbursementProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReimbursementProposal_ProvenanceRequired",
                table: "ReimbursementProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReimbursementProposal_RationaleRequired",
                table: "ReimbursementProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringItem_DeterministicMetadataRequired",
                table: "RecurringItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringItem_DeterministicThresholdRange",
                table: "RecurringItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringItem_DueWindowRange",
                table: "RecurringItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringItem_ScoreWeightsRange",
                table: "RecurringItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringItem_ScoreWeightsSum",
                table: "RecurringItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringItem_VarianceRange",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "LifecycleGroupId",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "LifecycleOrdinal",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "ProposalSource",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "ProvenancePayloadJson",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "ProvenanceReference",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "ProvenanceSource",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "StatusRationale",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "StatusReasonCode",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "SupersedesProposalId",
                table: "ReimbursementProposals");

            migrationBuilder.DropColumn(
                name: "AmountScoreWeight",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "AmountVarianceAbsolute",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "AmountVariancePercent",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "DeterministicMatchThreshold",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "DeterministicScoreVersion",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "DueDateScoreWeight",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "DueWindowDaysAfter",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "DueWindowDaysBefore",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "RecencyScoreWeight",
                table: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "TieBreakPolicy",
                table: "RecurringItems");
        }
    }
}

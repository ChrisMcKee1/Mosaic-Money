using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmBe14PlaidItemRecoveryWebhookStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryAction",
                table: "PlaidLinkSessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryReasonCode",
                table: "PlaidLinkSessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecoverySignaledAtUtc",
                table: "PlaidLinkSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryAction",
                table: "PlaidItemCredentials",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryReasonCode",
                table: "PlaidItemCredentials",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecoverySignaledAtUtc",
                table: "PlaidItemCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaidLinkSession_RecoveryActionAllowed",
                table: "PlaidLinkSessions",
                sql: "\"RecoveryAction\" IS NULL OR \"RecoveryAction\" IN ('none', 'requires_relink', 'requires_update_mode', 'needs_review')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaidLinkSession_RecoveryAudit",
                table: "PlaidLinkSessions",
                sql: "\"RecoveryAction\" IS NULL OR (\"RecoveryReasonCode\" IS NOT NULL AND LENGTH(TRIM(\"RecoveryReasonCode\")) > 0 AND \"RecoverySignaledAtUtc\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaidItemCredential_RecoveryActionAllowed",
                table: "PlaidItemCredentials",
                sql: "\"RecoveryAction\" IS NULL OR \"RecoveryAction\" IN ('none', 'requires_relink', 'requires_update_mode', 'needs_review')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaidItemCredential_RecoveryAudit",
                table: "PlaidItemCredentials",
                sql: "\"RecoveryAction\" IS NULL OR (\"RecoveryReasonCode\" IS NOT NULL AND LENGTH(TRIM(\"RecoveryReasonCode\")) > 0 AND \"RecoverySignaledAtUtc\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaidLinkSession_RecoveryActionAllowed",
                table: "PlaidLinkSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaidLinkSession_RecoveryAudit",
                table: "PlaidLinkSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaidItemCredential_RecoveryActionAllowed",
                table: "PlaidItemCredentials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaidItemCredential_RecoveryAudit",
                table: "PlaidItemCredentials");

            migrationBuilder.DropColumn(
                name: "RecoveryAction",
                table: "PlaidLinkSessions");

            migrationBuilder.DropColumn(
                name: "RecoveryReasonCode",
                table: "PlaidLinkSessions");

            migrationBuilder.DropColumn(
                name: "RecoverySignaledAtUtc",
                table: "PlaidLinkSessions");

            migrationBuilder.DropColumn(
                name: "RecoveryAction",
                table: "PlaidItemCredentials");

            migrationBuilder.DropColumn(
                name: "RecoveryReasonCode",
                table: "PlaidItemCredentials");

            migrationBuilder.DropColumn(
                name: "RecoverySignaledAtUtc",
                table: "PlaidItemCredentials");
        }
    }
}

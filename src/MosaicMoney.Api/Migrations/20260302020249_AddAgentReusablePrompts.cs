using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentReusablePrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentReusablePrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PromptText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StableKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    HouseholdUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentReusablePrompts", x => x.Id);
                    table.CheckConstraint("CK_AgentReusablePrompt_ArchiveAuditConsistency", "(\"IsArchived\" = FALSE AND \"ArchivedAtUtc\" IS NULL) OR (\"IsArchived\" = TRUE AND \"ArchivedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_AgentReusablePrompt_DisplayOrderRange", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_AgentReusablePrompt_FavoriteOnlyForUserScope", "(\"Scope\" = 1) OR (\"IsFavorite\" = FALSE)");
                    table.CheckConstraint("CK_AgentReusablePrompt_PromptTextRequired", "LENGTH(TRIM(\"PromptText\")) > 0");
                    table.CheckConstraint("CK_AgentReusablePrompt_ScopeOwnershipConsistency", "(\"Scope\" = 0 AND \"HouseholdId\" IS NULL AND \"HouseholdUserId\" IS NULL AND \"IsFavorite\" = FALSE) OR (\"Scope\" = 1 AND \"HouseholdId\" IS NOT NULL AND \"HouseholdUserId\" IS NOT NULL)");
                    table.CheckConstraint("CK_AgentReusablePrompt_ScopeRange", "\"Scope\" IN (0, 1)");
                    table.CheckConstraint("CK_AgentReusablePrompt_StableKeyForPlatform", "(\"Scope\" = 0 AND \"StableKey\" IS NOT NULL AND LENGTH(TRIM(\"StableKey\")) > 0) OR (\"Scope\" = 1)");
                    table.CheckConstraint("CK_AgentReusablePrompt_TitleRequired", "LENGTH(TRIM(\"Title\")) > 0");
                    table.CheckConstraint("CK_AgentReusablePrompt_UsageCountRange", "\"UsageCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_AgentReusablePrompts_HouseholdUsers_HouseholdUserId",
                        column: x => x.HouseholdUserId,
                        principalTable: "HouseholdUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentReusablePrompts_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentReusablePrompts_HouseholdId_HouseholdUserId_IsFavorite~",
                table: "AgentReusablePrompts",
                columns: new[] { "HouseholdId", "HouseholdUserId", "IsFavorite", "LastUsedAtUtc", "LastModifiedAtUtc" },
                filter: "\"Scope\" = 1 AND \"IsArchived\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReusablePrompts_HouseholdId_HouseholdUserId_Title",
                table: "AgentReusablePrompts",
                columns: new[] { "HouseholdId", "HouseholdUserId", "Title" },
                unique: true,
                filter: "\"Scope\" = 1 AND \"IsArchived\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReusablePrompts_HouseholdUserId",
                table: "AgentReusablePrompts",
                column: "HouseholdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReusablePrompts_Scope_IsArchived_DisplayOrder_Title",
                table: "AgentReusablePrompts",
                columns: new[] { "Scope", "IsArchived", "DisplayOrder", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentReusablePrompts_StableKey",
                table: "AgentReusablePrompts",
                column: "StableKey",
                unique: true,
                filter: "\"Scope\" = 0 AND \"StableKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentReusablePrompts");
        }
    }
}

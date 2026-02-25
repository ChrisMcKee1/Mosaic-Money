using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMemberAccessAclModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountMemberAccessEntries",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessRole = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Visibility = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMemberAccessEntries", x => new { x.AccountId, x.HouseholdUserId });
                    table.CheckConstraint("CK_AccountMemberAccess_AccessRoleRange", "\"AccessRole\" IN (0, 1, 2)");
                    table.CheckConstraint("CK_AccountMemberAccess_AccessVisibilityConsistency", "(\"AccessRole\" = 0 AND \"Visibility\" = 0) OR (\"AccessRole\" IN (1, 2) AND \"Visibility\" = 1)");
                    table.CheckConstraint("CK_AccountMemberAccess_VisibilityRange", "\"Visibility\" IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_AccountMemberAccessEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountMemberAccessEntries_HouseholdUsers_HouseholdUserId",
                        column: x => x.HouseholdUserId,
                        principalTable: "HouseholdUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMemberAccessEntries_HouseholdUserId_AccessRole_Visib~",
                table: "AccountMemberAccessEntries",
                columns: new[] { "HouseholdUserId", "AccessRole", "Visibility" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMemberAccessEntries");
        }
    }
}

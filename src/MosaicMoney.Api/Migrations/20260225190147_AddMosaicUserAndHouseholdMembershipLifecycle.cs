using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMosaicUserAndHouseholdMembershipLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAtUtc",
                table: "HouseholdUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvitedAtUtc",
                table: "HouseholdUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MembershipStatus",
                table: "HouseholdUsers",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "MosaicUserId",
                table: "HouseholdUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAtUtc",
                table: "HouseholdUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MosaicUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MosaicUsers", x => x.Id);
                    table.CheckConstraint("CK_MosaicUser_AuthProviderRequired", "LENGTH(TRIM(\"AuthProvider\")) > 0");
                    table.CheckConstraint("CK_MosaicUser_AuthSubjectRequired", "LENGTH(TRIM(\"AuthSubject\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdUsers_HouseholdId_MembershipStatus_MosaicUserId",
                table: "HouseholdUsers",
                columns: new[] { "HouseholdId", "MembershipStatus", "MosaicUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdUsers_HouseholdId_MosaicUserId",
                table: "HouseholdUsers",
                columns: new[] { "HouseholdId", "MosaicUserId" },
                unique: true,
                filter: "\"MembershipStatus\" = 1 AND \"MosaicUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdUsers_MosaicUserId",
                table: "HouseholdUsers",
                column: "MosaicUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdUser_ActivationAfterInvite",
                table: "HouseholdUsers",
                sql: "\"ActivatedAtUtc\" IS NULL OR \"InvitedAtUtc\" IS NULL OR \"ActivatedAtUtc\" >= \"InvitedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdUser_MembershipStatusRange",
                table: "HouseholdUsers",
                sql: "\"MembershipStatus\" IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdUser_RemovedAudit",
                table: "HouseholdUsers",
                sql: "(\"MembershipStatus\" = 3 AND \"RemovedAtUtc\" IS NOT NULL) OR (\"MembershipStatus\" <> 3 AND \"RemovedAtUtc\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicUsers_AuthProvider_AuthSubject",
                table: "MosaicUsers",
                columns: new[] { "AuthProvider", "AuthSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MosaicUsers_IsActive_LastSeenAtUtc",
                table: "MosaicUsers",
                columns: new[] { "IsActive", "LastSeenAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_HouseholdUsers_MosaicUsers_MosaicUserId",
                table: "HouseholdUsers",
                column: "MosaicUserId",
                principalTable: "MosaicUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HouseholdUsers_MosaicUsers_MosaicUserId",
                table: "HouseholdUsers");

            migrationBuilder.DropTable(
                name: "MosaicUsers");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdUsers_HouseholdId_MembershipStatus_MosaicUserId",
                table: "HouseholdUsers");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdUsers_HouseholdId_MosaicUserId",
                table: "HouseholdUsers");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdUsers_MosaicUserId",
                table: "HouseholdUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdUser_ActivationAfterInvite",
                table: "HouseholdUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdUser_MembershipStatusRange",
                table: "HouseholdUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdUser_RemovedAudit",
                table: "HouseholdUsers");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "HouseholdUsers");

            migrationBuilder.DropColumn(
                name: "InvitedAtUtc",
                table: "HouseholdUsers");

            migrationBuilder.DropColumn(
                name: "MembershipStatus",
                table: "HouseholdUsers");

            migrationBuilder.DropColumn(
                name: "MosaicUserId",
                table: "HouseholdUsers");

            migrationBuilder.DropColumn(
                name: "RemovedAtUtc",
                table: "HouseholdUsers");
        }
    }
}

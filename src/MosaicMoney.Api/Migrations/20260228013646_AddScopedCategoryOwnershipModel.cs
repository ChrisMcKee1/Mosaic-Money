using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedCategoryOwnershipModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerType",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "Name" },
                unique: true,
                filter: "\"OwnerType\" = 1 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_OwnerType_DisplayOrder_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "OwnerType", "DisplayOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_OwnerUserId_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "OwnerUserId", "Name" },
                unique: true,
                filter: "\"OwnerType\" = 2 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true,
                filter: "\"OwnerType\" = 0 AND \"HouseholdId\" IS NULL AND \"OwnerUserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_OwnerUserId",
                table: "Categories",
                column: "OwnerUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Category_OwnerScopeConsistency",
                table: "Categories",
                sql: "(\"OwnerType\" = 0 AND \"HouseholdId\" IS NULL AND \"OwnerUserId\" IS NULL) OR (\"OwnerType\" = 1 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NULL) OR (\"OwnerType\" = 2 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Category_OwnerTypeRange",
                table: "Categories",
                sql: "\"OwnerType\" IN (0, 1, 2)");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_HouseholdUsers_OwnerUserId",
                table: "Categories",
                column: "OwnerUserId",
                principalTable: "HouseholdUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_HouseholdUsers_OwnerUserId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_OwnerType_DisplayOrder_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_OwnerUserId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_OwnerUserId",
                table: "Categories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Category_OwnerScopeConsistency",
                table: "Categories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Category_OwnerTypeRange",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryLifecycleArchiveAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subcategories_CategoryId_Name",
                table: "Subcategories");

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

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "Subcategories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Subcategories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Subcategories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Subcategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAtUtc",
                table: "Subcategories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "Categories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAtUtc",
                table: "Categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(
                "UPDATE \"Categories\" SET \"CreatedAtUtc\" = NOW(), \"LastModifiedAtUtc\" = NOW() WHERE \"CreatedAtUtc\" = TIMESTAMPTZ '0001-01-01 00:00:00+00' OR \"LastModifiedAtUtc\" = TIMESTAMPTZ '0001-01-01 00:00:00+00';");

            migrationBuilder.Sql(
                "WITH ranked AS (SELECT \"Id\", ROW_NUMBER() OVER (PARTITION BY \"CategoryId\" ORDER BY \"Name\", \"Id\") - 1 AS \"RowOrder\" FROM \"Subcategories\") UPDATE \"Subcategories\" AS s SET \"DisplayOrder\" = ranked.\"RowOrder\", \"CreatedAtUtc\" = NOW(), \"LastModifiedAtUtc\" = NOW() FROM ranked WHERE s.\"Id\" = ranked.\"Id\" AND (s.\"CreatedAtUtc\" = TIMESTAMPTZ '0001-01-01 00:00:00+00' OR s.\"LastModifiedAtUtc\" = TIMESTAMPTZ '0001-01-01 00:00:00+00' OR s.\"DisplayOrder\" = 0);");

            migrationBuilder.CreateTable(
                name: "TaxonomyLifecycleAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ScopeOwnerType = table.Column<int>(type: "integer", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByHouseholdUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxonomyLifecycleAuditEntries", x => x.Id);
                    table.CheckConstraint("CK_TaxonomyLifecycleAuditEntry_EntityTypeRequired", "LENGTH(TRIM(\"EntityType\")) > 0");
                    table.CheckConstraint("CK_TaxonomyLifecycleAuditEntry_OperationRequired", "LENGTH(TRIM(\"Operation\")) > 0");
                    table.CheckConstraint("CK_TaxonomyLifecycleAuditEntry_ScopeOwnerTypeRange", "\"ScopeOwnerType\" IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_TaxonomyLifecycleAuditEntries_HouseholdUsers_PerformedByHou~",
                        column: x => x.PerformedByHouseholdUserId,
                        principalTable: "HouseholdUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_CategoryId_IsArchived_DisplayOrder_Name",
                table: "Subcategories",
                columns: new[] { "CategoryId", "IsArchived", "DisplayOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_CategoryId_Name",
                table: "Subcategories",
                columns: new[] { "CategoryId", "Name" },
                unique: true,
                filter: "\"IsArchived\" = FALSE");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subcategory_ArchiveAuditConsistency",
                table: "Subcategories",
                sql: "(\"IsArchived\" = FALSE AND \"ArchivedAtUtc\" IS NULL) OR (\"IsArchived\" = TRUE AND \"ArchivedAtUtc\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "Name" },
                unique: true,
                filter: "\"OwnerType\" = 1 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NULL AND \"IsArchived\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_OwnerType_IsArchived_DisplayOrder_Na~",
                table: "Categories",
                columns: new[] { "HouseholdId", "OwnerType", "IsArchived", "DisplayOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_OwnerUserId_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "OwnerUserId", "Name" },
                unique: true,
                filter: "\"OwnerType\" = 2 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NOT NULL AND \"IsArchived\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true,
                filter: "\"OwnerType\" = 0 AND \"HouseholdId\" IS NULL AND \"OwnerUserId\" IS NULL AND \"IsArchived\" = FALSE");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Category_ArchiveAuditConsistency",
                table: "Categories",
                sql: "(\"IsArchived\" = FALSE AND \"ArchivedAtUtc\" IS NULL) OR (\"IsArchived\" = TRUE AND \"ArchivedAtUtc\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyLifecycleAuditEntries_EntityType_EntityId_Performed~",
                table: "TaxonomyLifecycleAuditEntries",
                columns: new[] { "EntityType", "EntityId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyLifecycleAuditEntries_PerformedByHouseholdUserId_Pe~",
                table: "TaxonomyLifecycleAuditEntries",
                columns: new[] { "PerformedByHouseholdUserId", "PerformedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxonomyLifecycleAuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_Subcategories_CategoryId_IsArchived_DisplayOrder_Name",
                table: "Subcategories");

            migrationBuilder.DropIndex(
                name: "IX_Subcategories_CategoryId_Name",
                table: "Subcategories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subcategory_ArchiveAuditConsistency",
                table: "Subcategories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_OwnerType_IsArchived_DisplayOrder_Na~",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_OwnerUserId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Category_ArchiveAuditConsistency",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "LastModifiedAtUtc",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "LastModifiedAtUtc",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_CategoryId_Name",
                table: "Subcategories",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

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
        }
    }
}

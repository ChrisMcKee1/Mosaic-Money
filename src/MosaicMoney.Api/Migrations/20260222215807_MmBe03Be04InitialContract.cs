using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmBe03Be04InitialContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subcategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsBusinessExpense = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subcategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalAccountKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalUserKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdUsers_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsVariable = table.Column<bool>(type: "boolean", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UserNote = table.Column<string>(type: "text", nullable: true),
                    AgentNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringItems_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnrichedTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubcategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    NeedsReviewByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlaidTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DescriptionEmbedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    ExcludeFromBudget = table.Column<bool>(type: "boolean", nullable: false),
                    IsExtraPrincipal = table.Column<bool>(type: "boolean", nullable: false),
                    UserNote = table.Column<string>(type: "text", nullable: true),
                    AgentNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrichedTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnrichedTransactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnrichedTransactions_HouseholdUsers_NeedsReviewByUserId",
                        column: x => x.NeedsReviewByUserId,
                        principalTable: "HouseholdUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnrichedTransactions_RecurringItems_RecurringItemId",
                        column: x => x.RecurringItemId,
                        principalTable: "RecurringItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnrichedTransactions_Subcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TransactionSplits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmortizationMonths = table.Column<int>(type: "integer", nullable: false),
                    UserNote = table.Column<string>(type: "text", nullable: true),
                    AgentNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionSplits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionSplits_EnrichedTransactions_ParentTransactionId",
                        column: x => x.ParentTransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionSplits_Subcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReimbursementProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomingTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedTransactionSplitId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecisionedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserNote = table.Column<string>(type: "text", nullable: true),
                    AgentNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReimbursementProposals", x => x.Id);
                    table.CheckConstraint("CK_ReimbursementProposal_OneRelatedTarget", "(\"RelatedTransactionId\" IS NOT NULL AND \"RelatedTransactionSplitId\" IS NULL) OR (\"RelatedTransactionId\" IS NULL AND \"RelatedTransactionSplitId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ReimbursementProposals_EnrichedTransactions_IncomingTransac~",
                        column: x => x.IncomingTransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReimbursementProposals_EnrichedTransactions_RelatedTransact~",
                        column: x => x.RelatedTransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReimbursementProposals_HouseholdUsers_DecisionedByUserId",
                        column: x => x.DecisionedByUserId,
                        principalTable: "HouseholdUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReimbursementProposals_TransactionSplits_RelatedTransaction~",
                        column: x => x.RelatedTransactionSplitId,
                        principalTable: "TransactionSplits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_HouseholdId_ExternalAccountKey",
                table: "Accounts",
                columns: new[] { "HouseholdId", "ExternalAccountKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_AccountId",
                table: "EnrichedTransactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_DescriptionEmbedding",
                table: "EnrichedTransactions",
                column: "DescriptionEmbedding",
                filter: "\"DescriptionEmbedding\" IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_NeedsReviewByUserId",
                table: "EnrichedTransactions",
                column: "NeedsReviewByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_PlaidTransactionId",
                table: "EnrichedTransactions",
                column: "PlaidTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_RecurringItemId_TransactionDate",
                table: "EnrichedTransactions",
                columns: new[] { "RecurringItemId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_ReviewStatus_NeedsReviewByUserId_Trans~",
                table: "EnrichedTransactions",
                columns: new[] { "ReviewStatus", "NeedsReviewByUserId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_SubcategoryId",
                table: "EnrichedTransactions",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdUsers_HouseholdId_ExternalUserKey",
                table: "HouseholdUsers",
                columns: new[] { "HouseholdId", "ExternalUserKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringItems_HouseholdId",
                table: "RecurringItems",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_DecisionedByUserId",
                table: "ReimbursementProposals",
                column: "DecisionedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_IncomingTransactionId_Status",
                table: "ReimbursementProposals",
                columns: new[] { "IncomingTransactionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_RelatedTransactionId",
                table: "ReimbursementProposals",
                column: "RelatedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_RelatedTransactionSplitId",
                table: "ReimbursementProposals",
                column: "RelatedTransactionSplitId");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_Status_CreatedAtUtc",
                table: "ReimbursementProposals",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_CategoryId_Name",
                table: "Subcategories",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSplits_ParentTransactionId",
                table: "TransactionSplits",
                column: "ParentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSplits_SubcategoryId",
                table: "TransactionSplits",
                column: "SubcategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReimbursementProposals");

            migrationBuilder.DropTable(
                name: "TransactionSplits");

            migrationBuilder.DropTable(
                name: "EnrichedTransactions");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "HouseholdUsers");

            migrationBuilder.DropTable(
                name: "RecurringItems");

            migrationBuilder.DropTable(
                name: "Subcategories");

            migrationBuilder.DropTable(
                name: "Households");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmBe10AsyncEmbeddingsQueuePipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionEmbeddingHash",
                table: "EnrichedTransactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionEmbeddingQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    EnqueuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAttemptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionEmbeddingQueueItems", x => x.Id);
                    table.CheckConstraint("CK_TransactionEmbeddingQueueItem_AttemptBoundedByMax", "\"AttemptCount\" <= \"MaxAttempts\"");
                    table.CheckConstraint("CK_TransactionEmbeddingQueueItem_AttemptCountRange", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_TransactionEmbeddingQueueItem_MaxAttemptsRange", "\"MaxAttempts\" >= 1");
                    table.ForeignKey(
                        name: "FK_TransactionEmbeddingQueueItems_EnrichedTransactions_Transac~",
                        column: x => x.TransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedTransactions_DescriptionEmbeddingHash",
                table: "EnrichedTransactions",
                column: "DescriptionEmbeddingHash");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionEmbeddingQueueItems_Status_NextAttemptAtUtc_Enqu~",
                table: "TransactionEmbeddingQueueItems",
                columns: new[] { "Status", "NextAttemptAtUtc", "EnqueuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionEmbeddingQueueItems_TransactionId_DescriptionHash",
                table: "TransactionEmbeddingQueueItems",
                columns: new[] { "TransactionId", "DescriptionHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionEmbeddingQueueItems");

            migrationBuilder.DropIndex(
                name: "IX_EnrichedTransactions_DescriptionEmbeddingHash",
                table: "EnrichedTransactions");

            migrationBuilder.DropColumn(
                name: "DescriptionEmbeddingHash",
                table: "EnrichedTransactions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class MmBe06IdempotentIngestionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawTransactionIngestionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeltaCursor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnrichedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastDisposition = table.Column<int>(type: "integer", nullable: false),
                    LastReviewReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawTransactionIngestionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawTransactionIngestionRecords_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RawTransactionIngestionRecords_EnrichedTransactions_Enriche~",
                        column: x => x.EnrichedTransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawTransactionIngestionRecords_AccountId",
                table: "RawTransactionIngestionRecords",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RawTransactionIngestionRecords_EnrichedTransactionId",
                table: "RawTransactionIngestionRecords",
                column: "EnrichedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RawTransactionIngestionRecords_Source_DeltaCursor_SourceTra~",
                table: "RawTransactionIngestionRecords",
                columns: new[] { "Source", "DeltaCursor", "SourceTransactionId", "PayloadHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawTransactionIngestionRecords_Source_SourceTransactionId_L~",
                table: "RawTransactionIngestionRecords",
                columns: new[] { "Source", "SourceTransactionId", "LastProcessedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawTransactionIngestionRecords");
        }
    }
}

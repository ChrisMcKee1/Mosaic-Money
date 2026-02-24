using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace MosaicMoney.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:azure_ai", ",,")
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
                name: "PlaidItemCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlaidEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccessTokenCiphertext = table.Column<string>(type: "text", nullable: false),
                    AccessTokenFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRotatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastLinkedSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastClientMetadataJson = table.Column<string>(type: "text", nullable: true),
                    RecoveryAction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RecoveryReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RecoverySignaledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidItemCredentials", x => x.Id);
                    table.CheckConstraint("CK_PlaidItemCredential_AccessTokenCiphertextRequired", "LENGTH(TRIM(\"AccessTokenCiphertext\")) > 0");
                    table.CheckConstraint("CK_PlaidItemCredential_AccessTokenFingerprintRequired", "LENGTH(TRIM(\"AccessTokenFingerprint\")) > 0");
                    table.CheckConstraint("CK_PlaidItemCredential_ItemIdRequired", "LENGTH(TRIM(\"ItemId\")) > 0");
                    table.CheckConstraint("CK_PlaidItemCredential_RecoveryActionAllowed", "\"RecoveryAction\" IS NULL OR \"RecoveryAction\" IN ('none', 'requires_relink', 'requires_update_mode', 'needs_review')");
                    table.CheckConstraint("CK_PlaidItemCredential_RecoveryAudit", "\"RecoveryAction\" IS NULL OR (\"RecoveryReasonCode\" IS NOT NULL AND LENGTH(TRIM(\"RecoveryReasonCode\")) > 0 AND \"RecoverySignaledAtUtc\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "PlaidItemSyncStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlaidEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InitialUpdateComplete = table.Column<bool>(type: "boolean", nullable: false),
                    HistoricalUpdateComplete = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastWebhookAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastSyncErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastSyncErrorAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncStatus = table.Column<int>(type: "integer", nullable: false),
                    PendingWebhookCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidItemSyncStates", x => x.Id);
                    table.CheckConstraint("CK_PlaidItemSyncState_CursorRequired", "LENGTH(TRIM(\"Cursor\")) > 0");
                    table.CheckConstraint("CK_PlaidItemSyncState_ItemIdRequired", "LENGTH(TRIM(\"ItemId\")) > 0");
                    table.CheckConstraint("CK_PlaidItemSyncState_LastSyncErrorAudit", "(\"LastSyncErrorCode\" IS NULL AND \"LastSyncErrorAtUtc\" IS NULL) OR (\"LastSyncErrorCode\" IS NOT NULL AND LENGTH(TRIM(\"LastSyncErrorCode\")) > 0 AND \"LastSyncErrorAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_PlaidItemSyncState_PendingWebhookCountRange", "\"PendingWebhookCount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PlaidLinkSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LinkTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OAuthStateId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedProducts = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedEnvironment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LinkTokenCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkTokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEventAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastClientMetadataJson = table.Column<string>(type: "text", nullable: true),
                    RecoveryAction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RecoveryReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RecoverySignaledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LinkedItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidLinkSessions", x => x.Id);
                    table.CheckConstraint("CK_PlaidLinkSession_ClientUserIdRequired", "LENGTH(TRIM(\"ClientUserId\")) > 0");
                    table.CheckConstraint("CK_PlaidLinkSession_LinkTokenHashRequired", "LENGTH(TRIM(\"LinkTokenHash\")) > 0");
                    table.CheckConstraint("CK_PlaidLinkSession_RecoveryActionAllowed", "\"RecoveryAction\" IS NULL OR \"RecoveryAction\" IN ('none', 'requires_relink', 'requires_update_mode', 'needs_review')");
                    table.CheckConstraint("CK_PlaidLinkSession_RecoveryAudit", "\"RecoveryAction\" IS NULL OR (\"RecoveryReasonCode\" IS NOT NULL AND LENGTH(TRIM(\"RecoveryReasonCode\")) > 0 AND \"RecoverySignaledAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_PlaidLinkSession_RequestedProductsRequired", "LENGTH(TRIM(\"RequestedProducts\")) > 0");
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
                    DueWindowDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    DueWindowDaysAfter = table.Column<int>(type: "integer", nullable: false),
                    AmountVariancePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AmountVarianceAbsolute = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeterministicMatchThreshold = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    DueDateScoreWeight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    AmountScoreWeight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    RecencyScoreWeight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    DeterministicScoreVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TieBreakPolicy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UserNote = table.Column<string>(type: "text", nullable: true),
                    AgentNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringItems", x => x.Id);
                    table.CheckConstraint("CK_RecurringItem_DeterministicMetadataRequired", "LENGTH(TRIM(\"DeterministicScoreVersion\")) > 0 AND LENGTH(TRIM(\"TieBreakPolicy\")) > 0");
                    table.CheckConstraint("CK_RecurringItem_DeterministicThresholdRange", "\"DeterministicMatchThreshold\" >= 0 AND \"DeterministicMatchThreshold\" <= 1");
                    table.CheckConstraint("CK_RecurringItem_DueWindowRange", "\"DueWindowDaysBefore\" >= 0 AND \"DueWindowDaysBefore\" <= 90 AND \"DueWindowDaysAfter\" >= 0 AND \"DueWindowDaysAfter\" <= 90");
                    table.CheckConstraint("CK_RecurringItem_ScoreWeightsRange", "\"DueDateScoreWeight\" >= 0 AND \"DueDateScoreWeight\" <= 1 AND \"AmountScoreWeight\" >= 0 AND \"AmountScoreWeight\" <= 1 AND \"RecencyScoreWeight\" >= 0 AND \"RecencyScoreWeight\" <= 1");
                    table.CheckConstraint("CK_RecurringItem_ScoreWeightsSum", "ROUND(\"DueDateScoreWeight\" + \"AmountScoreWeight\" + \"RecencyScoreWeight\", 4) = 1");
                    table.CheckConstraint("CK_RecurringItem_VarianceRange", "\"AmountVariancePercent\" >= 0 AND \"AmountVariancePercent\" <= 100 AND \"AmountVarianceAbsolute\" >= 0");
                    table.ForeignKey(
                        name: "FK_RecurringItems_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaidLinkSessionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaidLinkSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClientMetadataJson = table.Column<string>(type: "text", nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidLinkSessionEvents", x => x.Id);
                    table.CheckConstraint("CK_PlaidLinkSessionEvent_EventTypeRequired", "LENGTH(TRIM(\"EventType\")) > 0");
                    table.ForeignKey(
                        name: "FK_PlaidLinkSessionEvents_PlaidLinkSessions_PlaidLinkSessionId",
                        column: x => x.PlaidLinkSessionId,
                        principalTable: "PlaidLinkSessions",
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
                    DescriptionEmbeddingHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "TransactionClassificationOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedSubcategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false),
                    DecisionReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DecisionRationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AgentNoteSummary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionClassificationOutcomes", x => x.Id);
                    table.CheckConstraint("CK_TransactionClassificationOutcome_DecisionReviewRouting", "(\"Decision\" = 2 AND \"ReviewStatus\" = 1) OR (\"Decision\" = 1 AND \"ReviewStatus\" <> 1)");
                    table.CheckConstraint("CK_TransactionClassificationOutcome_FinalConfidenceRange", "\"FinalConfidence\" >= 0 AND \"FinalConfidence\" <= 1");
                    table.ForeignKey(
                        name: "FK_TransactionClassificationOutcomes_EnrichedTransactions_Tran~",
                        column: x => x.TransactionId,
                        principalTable: "EnrichedTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionClassificationOutcomes_Subcategories_ProposedSub~",
                        column: x => x.ProposedSubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                name: "ClassificationStageOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    StageOrder = table.Column<int>(type: "integer", nullable: false),
                    ProposedSubcategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    RationaleCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EscalatedToNextStage = table.Column<bool>(type: "boolean", nullable: false),
                    ProducedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationStageOutputs", x => x.Id);
                    table.CheckConstraint("CK_ClassificationStageOutput_ConfidenceRange", "\"Confidence\" >= 0 AND \"Confidence\" <= 1");
                    table.CheckConstraint("CK_ClassificationStageOutput_StageOrderRange", "\"StageOrder\" >= 1 AND \"StageOrder\" <= 3");
                    table.ForeignKey(
                        name: "FK_ClassificationStageOutputs_Subcategories_ProposedSubcategor~",
                        column: x => x.ProposedSubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassificationStageOutputs_TransactionClassificationOutcome~",
                        column: x => x.OutcomeId,
                        principalTable: "TransactionClassificationOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    LifecycleGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StatusRationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProposalSource = table.Column<int>(type: "integer", nullable: false),
                    ProvenanceSource = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProvenanceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProvenancePayloadJson = table.Column<string>(type: "text", nullable: true),
                    SupersedesProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserNote = table.Column<string>(type: "text", nullable: true),
                    AgentNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReimbursementProposals", x => x.Id);
                    table.CheckConstraint("CK_ReimbursementProposal_DecisionAuditForFinalStates", "(\"Status\" IN (2, 3) AND \"DecisionedByUserId\" IS NOT NULL AND \"DecisionedAtUtc\" IS NOT NULL) OR (\"Status\" NOT IN (2, 3))");
                    table.CheckConstraint("CK_ReimbursementProposal_LifecycleOrdinal", "\"LifecycleOrdinal\" >= 1");
                    table.CheckConstraint("CK_ReimbursementProposal_OneRelatedTarget", "(\"RelatedTransactionId\" IS NOT NULL AND \"RelatedTransactionSplitId\" IS NULL) OR (\"RelatedTransactionId\" IS NULL AND \"RelatedTransactionSplitId\" IS NOT NULL)");
                    table.CheckConstraint("CK_ReimbursementProposal_ProvenanceRequired", "LENGTH(TRIM(\"ProvenanceSource\")) > 0");
                    table.CheckConstraint("CK_ReimbursementProposal_RationaleRequired", "LENGTH(TRIM(\"StatusReasonCode\")) > 0 AND LENGTH(TRIM(\"StatusRationale\")) > 0");
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
                        name: "FK_ReimbursementProposals_ReimbursementProposals_SupersedesPro~",
                        column: x => x.SupersedesProposalId,
                        principalTable: "ReimbursementProposals",
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
                name: "IX_ClassificationStageOutputs_OutcomeId_Stage",
                table: "ClassificationStageOutputs",
                columns: new[] { "OutcomeId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationStageOutputs_ProposedSubcategoryId",
                table: "ClassificationStageOutputs",
                column: "ProposedSubcategoryId");

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
                name: "IX_EnrichedTransactions_DescriptionEmbeddingHash",
                table: "EnrichedTransactions",
                column: "DescriptionEmbeddingHash");

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
                name: "IX_PlaidItemCredentials_HouseholdId_Status_LastRotatedAtUtc",
                table: "PlaidItemCredentials",
                columns: new[] { "HouseholdId", "Status", "LastRotatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItemCredentials_PlaidEnvironment_ItemId",
                table: "PlaidItemCredentials",
                columns: new[] { "PlaidEnvironment", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItemSyncStates_PlaidEnvironment_ItemId",
                table: "PlaidItemSyncStates",
                columns: new[] { "PlaidEnvironment", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItemSyncStates_SyncStatus_LastWebhookAtUtc_LastSyncedA~",
                table: "PlaidItemSyncStates",
                columns: new[] { "SyncStatus", "LastWebhookAtUtc", "LastSyncedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidLinkSessionEvents_PlaidLinkSessionId_OccurredAtUtc",
                table: "PlaidLinkSessionEvents",
                columns: new[] { "PlaidLinkSessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidLinkSessions_HouseholdId_LinkTokenCreatedAtUtc",
                table: "PlaidLinkSessions",
                columns: new[] { "HouseholdId", "LinkTokenCreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidLinkSessions_LinkTokenHash",
                table: "PlaidLinkSessions",
                column: "LinkTokenHash",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_RecurringItems_HouseholdId",
                table: "RecurringItems",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_DecisionedByUserId",
                table: "ReimbursementProposals",
                column: "DecisionedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_IncomingTransactionId_LifecycleGroup~",
                table: "ReimbursementProposals",
                columns: new[] { "IncomingTransactionId", "LifecycleGroupId", "LifecycleOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_IncomingTransactionId_Status",
                table: "ReimbursementProposals",
                columns: new[] { "IncomingTransactionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementProposals_LifecycleGroupId_CreatedAtUtc",
                table: "ReimbursementProposals",
                columns: new[] { "LifecycleGroupId", "CreatedAtUtc" });

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
                name: "IX_ReimbursementProposals_SupersedesProposalId",
                table: "ReimbursementProposals",
                column: "SupersedesProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_CategoryId_Name",
                table: "Subcategories",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationOutcomes_Decision_ReviewStatus_Cre~",
                table: "TransactionClassificationOutcomes",
                columns: new[] { "Decision", "ReviewStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationOutcomes_ProposedSubcategoryId",
                table: "TransactionClassificationOutcomes",
                column: "ProposedSubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationOutcomes_TransactionId_CreatedAtUtc",
                table: "TransactionClassificationOutcomes",
                columns: new[] { "TransactionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionEmbeddingQueueItems_Status_NextAttemptAtUtc_Enqu~",
                table: "TransactionEmbeddingQueueItems",
                columns: new[] { "Status", "NextAttemptAtUtc", "EnqueuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionEmbeddingQueueItems_TransactionId_DescriptionHash",
                table: "TransactionEmbeddingQueueItems",
                columns: new[] { "TransactionId", "DescriptionHash" },
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
                name: "ClassificationStageOutputs");

            migrationBuilder.DropTable(
                name: "PlaidItemCredentials");

            migrationBuilder.DropTable(
                name: "PlaidItemSyncStates");

            migrationBuilder.DropTable(
                name: "PlaidLinkSessionEvents");

            migrationBuilder.DropTable(
                name: "RawTransactionIngestionRecords");

            migrationBuilder.DropTable(
                name: "ReimbursementProposals");

            migrationBuilder.DropTable(
                name: "TransactionEmbeddingQueueItems");

            migrationBuilder.DropTable(
                name: "TransactionClassificationOutcomes");

            migrationBuilder.DropTable(
                name: "PlaidLinkSessions");

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

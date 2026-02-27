using System;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Domain.Ledger;
using Pgvector.EntityFrameworkCore;

namespace MosaicMoney.Api.Data;

public sealed class MosaicMoneyDbContext : DbContext
{
    public MosaicMoneyDbContext(DbContextOptions<MosaicMoneyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();

    public DbSet<MosaicUser> MosaicUsers => Set<MosaicUser>();

    public DbSet<HouseholdUser> HouseholdUsers => Set<HouseholdUser>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountMemberAccess> AccountMemberAccessEntries => Set<AccountMemberAccess>();

    public DbSet<AccountAccessPolicyReviewQueueEntry> AccountAccessPolicyReviewQueueEntries => Set<AccountAccessPolicyReviewQueueEntry>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Subcategory> Subcategories => Set<Subcategory>();

    public DbSet<RecurringItem> RecurringItems => Set<RecurringItem>();

    public DbSet<EnrichedTransaction> EnrichedTransactions => Set<EnrichedTransaction>();

    public DbSet<TransactionEmbeddingQueueItem> TransactionEmbeddingQueueItems => Set<TransactionEmbeddingQueueItem>();

    public DbSet<RawTransactionIngestionRecord> RawTransactionIngestionRecords => Set<RawTransactionIngestionRecord>();

    public DbSet<PlaidLinkSession> PlaidLinkSessions => Set<PlaidLinkSession>();

    public DbSet<PlaidLinkSessionEvent> PlaidLinkSessionEvents => Set<PlaidLinkSessionEvent>();

    public DbSet<PlaidItemCredential> PlaidItemCredentials => Set<PlaidItemCredential>();

    public DbSet<PlaidAccountLink> PlaidAccountLinks => Set<PlaidAccountLink>();

    public DbSet<PlaidItemSyncState> PlaidItemSyncStates => Set<PlaidItemSyncState>();

    public DbSet<LiabilityAccount> LiabilityAccounts => Set<LiabilityAccount>();

    public DbSet<LiabilitySnapshot> LiabilitySnapshots => Set<LiabilitySnapshot>();

    public DbSet<InvestmentAccount> InvestmentAccounts => Set<InvestmentAccount>();

    public DbSet<InvestmentHoldingSnapshot> InvestmentHoldingSnapshots => Set<InvestmentHoldingSnapshot>();

    public DbSet<InvestmentTransaction> InvestmentTransactions => Set<InvestmentTransaction>();

    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();

    public DbSet<ReimbursementProposal> ReimbursementProposals => Set<ReimbursementProposal>();

    public DbSet<TransactionClassificationOutcome> TransactionClassificationOutcomes => Set<TransactionClassificationOutcome>();

    public DbSet<ClassificationStageOutput> ClassificationStageOutputs => Set<ClassificationStageOutput>();

    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    public DbSet<AgentRunStage> AgentRunStages => Set<AgentRunStage>();

    public DbSet<AgentSignal> AgentSignals => Set<AgentSignal>();

    public DbSet<AgentDecisionAudit> AgentDecisionAudit => Set<AgentDecisionAudit>();

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isNpgsqlProvider = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        if (isNpgsqlProvider)
        {
            modelBuilder.HasPostgresExtension("vector");
            modelBuilder.HasPostgresExtension("azure_ai");
        }

        modelBuilder.Entity<Category>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<MosaicUser>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_MosaicUser_AuthProviderRequired",
                    "LENGTH(TRIM(\"AuthProvider\")) > 0");

                t.HasCheckConstraint(
                    "CK_MosaicUser_AuthSubjectRequired",
                    "LENGTH(TRIM(\"AuthSubject\")) > 0");
            });

        modelBuilder.Entity<MosaicUser>()
            .HasIndex(x => new { x.AuthProvider, x.AuthSubject })
            .IsUnique();

        modelBuilder.Entity<MosaicUser>()
            .HasIndex(x => new { x.IsActive, x.LastSeenAtUtc });

        modelBuilder.Entity<Subcategory>()
            .HasIndex(x => new { x.CategoryId, x.Name })
            .IsUnique();

        modelBuilder.Entity<HouseholdUser>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_HouseholdUser_MembershipStatusRange",
                    "\"MembershipStatus\" IN (1, 2, 3)");

                t.HasCheckConstraint(
                    "CK_HouseholdUser_RemovedAudit",
                    "(\"MembershipStatus\" = 3 AND \"RemovedAtUtc\" IS NOT NULL) OR (\"MembershipStatus\" <> 3 AND \"RemovedAtUtc\" IS NULL)");

                t.HasCheckConstraint(
                    "CK_HouseholdUser_ActivationAfterInvite",
                    "\"ActivatedAtUtc\" IS NULL OR \"InvitedAtUtc\" IS NULL OR \"ActivatedAtUtc\" >= \"InvitedAtUtc\"");
            });

        modelBuilder.Entity<HouseholdUser>()
            .HasIndex(x => new { x.HouseholdId, x.ExternalUserKey })
            .IsUnique();

        modelBuilder.Entity<HouseholdUser>()
            .Property(x => x.MembershipStatus)
            .HasDefaultValue(HouseholdMembershipStatus.Active);

        modelBuilder.Entity<HouseholdUser>()
            .HasIndex(x => new { x.HouseholdId, x.MembershipStatus, x.MosaicUserId });

        modelBuilder.Entity<HouseholdUser>()
            .HasIndex(x => new { x.HouseholdId, x.MosaicUserId })
            .IsUnique()
            .HasFilter("\"MembershipStatus\" = 1 AND \"MosaicUserId\" IS NOT NULL");

        modelBuilder.Entity<Account>()
            .HasIndex(x => new { x.HouseholdId, x.ExternalAccountKey })
            .IsUnique();

        modelBuilder.Entity<Account>()
            .Property(x => x.AccessPolicyNeedsReview)
            .HasDefaultValue(false);

        modelBuilder.Entity<AccountMemberAccess>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_AccountMemberAccess_AccessRoleRange",
                    "\"AccessRole\" IN (0, 1, 2)");

                t.HasCheckConstraint(
                    "CK_AccountMemberAccess_VisibilityRange",
                    "\"Visibility\" IN (0, 1)");

                t.HasCheckConstraint(
                    "CK_AccountMemberAccess_AccessVisibilityConsistency",
                    "(\"AccessRole\" = 0 AND \"Visibility\" = 0) OR (\"AccessRole\" IN (1, 2) AND \"Visibility\" = 1)");
            });

        modelBuilder.Entity<AccountMemberAccess>()
            .HasKey(x => new { x.AccountId, x.HouseholdUserId });

        modelBuilder.Entity<AccountMemberAccess>()
            .Property(x => x.AccessRole)
            .HasDefaultValue(AccountAccessRole.None);

        modelBuilder.Entity<AccountMemberAccess>()
            .Property(x => x.Visibility)
            .HasDefaultValue(AccountAccessVisibility.Hidden);

        modelBuilder.Entity<AccountMemberAccess>()
            .HasIndex(x => new { x.HouseholdUserId, x.AccessRole, x.Visibility });

        modelBuilder.Entity<AccountAccessPolicyReviewQueueEntry>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_AccountAccessPolicyReviewQueueEntry_ReasonCodeRequired",
                    "LENGTH(TRIM(\"ReasonCode\")) > 0");

                t.HasCheckConstraint(
                    "CK_AccountAccessPolicyReviewQueueEntry_RationaleRequired",
                    "LENGTH(TRIM(\"Rationale\")) > 0");

                t.HasCheckConstraint(
                    "CK_AccountAccessPolicyReviewQueueEntry_EvaluationAfterEnqueue",
                    "\"LastEvaluatedAtUtc\" >= \"EnqueuedAtUtc\"");

                t.HasCheckConstraint(
                    "CK_AccountAccessPolicyReviewQueueEntry_ResolutionAfterEnqueue",
                    "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"EnqueuedAtUtc\"");
            });

        modelBuilder.Entity<AccountAccessPolicyReviewQueueEntry>()
            .HasKey(x => x.AccountId);

        modelBuilder.Entity<AccountAccessPolicyReviewQueueEntry>()
            .HasIndex(x => new { x.HouseholdId, x.ResolvedAtUtc, x.LastEvaluatedAtUtc });

        modelBuilder.Entity<AccountAccessPolicyReviewQueueEntry>()
            .HasOne(x => x.Account)
            .WithOne(x => x.AccessPolicyReviewQueueEntry)
            .HasForeignKey<AccountAccessPolicyReviewQueueEntry>(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccountAccessPolicyReviewQueueEntry>()
            .HasOne(x => x.Household)
            .WithMany(x => x.AccountAccessPolicyReviewQueueEntries)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EnrichedTransaction>()
            .HasIndex(x => x.PlaidTransactionId)
            .IsUnique();

        if (isNpgsqlProvider)
        {
            modelBuilder.Entity<EnrichedTransaction>()
                .Property(x => x.DescriptionEmbedding)
                .HasColumnType("vector(1536)");

            modelBuilder.Entity<EnrichedTransaction>()
                .HasIndex(x => x.DescriptionEmbedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasFilter("\"DescriptionEmbedding\" IS NOT NULL");
        }
        else
        {
            modelBuilder.Entity<EnrichedTransaction>()
                .Ignore(x => x.DescriptionEmbedding);
        }

        modelBuilder.Entity<EnrichedTransaction>()
            .HasIndex(x => new { x.ReviewStatus, x.NeedsReviewByUserId, x.TransactionDate });

        modelBuilder.Entity<EnrichedTransaction>()
            .HasIndex(x => x.DescriptionEmbeddingHash);

        modelBuilder.Entity<EnrichedTransaction>()
            .HasIndex(x => new { x.RecurringItemId, x.TransactionDate });

        modelBuilder.Entity<RecurringItem>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_RecurringItem_DueWindowRange",
                    "\"DueWindowDaysBefore\" >= 0 AND \"DueWindowDaysBefore\" <= 90 AND \"DueWindowDaysAfter\" >= 0 AND \"DueWindowDaysAfter\" <= 90");

                t.HasCheckConstraint(
                    "CK_RecurringItem_VarianceRange",
                    "\"AmountVariancePercent\" >= 0 AND \"AmountVariancePercent\" <= 100 AND \"AmountVarianceAbsolute\" >= 0");

                t.HasCheckConstraint(
                    "CK_RecurringItem_DeterministicThresholdRange",
                    "\"DeterministicMatchThreshold\" >= 0 AND \"DeterministicMatchThreshold\" <= 1");

                t.HasCheckConstraint(
                    "CK_RecurringItem_ScoreWeightsRange",
                    "\"DueDateScoreWeight\" >= 0 AND \"DueDateScoreWeight\" <= 1 AND \"AmountScoreWeight\" >= 0 AND \"AmountScoreWeight\" <= 1 AND \"RecencyScoreWeight\" >= 0 AND \"RecencyScoreWeight\" <= 1");

                t.HasCheckConstraint(
                    "CK_RecurringItem_ScoreWeightsSum",
                    "ROUND(\"DueDateScoreWeight\" + \"AmountScoreWeight\" + \"RecencyScoreWeight\", 4) = 1");

                t.HasCheckConstraint(
                    "CK_RecurringItem_DeterministicMetadataRequired",
                    "LENGTH(TRIM(\"DeterministicScoreVersion\")) > 0 AND LENGTH(TRIM(\"TieBreakPolicy\")) > 0");
            });

        modelBuilder.Entity<RawTransactionIngestionRecord>()
            .HasIndex(x => new { x.Source, x.DeltaCursor, x.SourceTransactionId, x.PayloadHash })
            .IsUnique();

        modelBuilder.Entity<RawTransactionIngestionRecord>()
            .HasIndex(x => new { x.Source, x.SourceTransactionId, x.LastProcessedAtUtc });

        modelBuilder.Entity<RawTransactionIngestionRecord>()
            .HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RawTransactionIngestionRecord>()
            .HasOne(x => x.EnrichedTransaction)
            .WithMany(x => x.RawIngestionRecords)
            .HasForeignKey(x => x.EnrichedTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TransactionEmbeddingQueueItem>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_TransactionEmbeddingQueueItem_AttemptCountRange",
                    "\"AttemptCount\" >= 0");

                t.HasCheckConstraint(
                    "CK_TransactionEmbeddingQueueItem_MaxAttemptsRange",
                    "\"MaxAttempts\" >= 1");

                t.HasCheckConstraint(
                    "CK_TransactionEmbeddingQueueItem_AttemptBoundedByMax",
                    "\"AttemptCount\" <= \"MaxAttempts\"");
            });

        modelBuilder.Entity<TransactionEmbeddingQueueItem>()
            .HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.EnqueuedAtUtc });

        modelBuilder.Entity<TransactionEmbeddingQueueItem>()
            .HasIndex(x => new { x.TransactionId, x.DescriptionHash })
            .IsUnique();

        modelBuilder.Entity<TransactionEmbeddingQueueItem>()
            .HasOne(x => x.Transaction)
            .WithMany(x => x.EmbeddingQueueItems)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaidLinkSession>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PlaidLinkSession_LinkTokenHashRequired",
                    "LENGTH(TRIM(\"LinkTokenHash\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidLinkSession_ClientUserIdRequired",
                    "LENGTH(TRIM(\"ClientUserId\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidLinkSession_RequestedProductsRequired",
                    "LENGTH(TRIM(\"RequestedProducts\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidLinkSession_RecoveryAudit",
                    "\"RecoveryAction\" IS NULL OR (\"RecoveryReasonCode\" IS NOT NULL AND LENGTH(TRIM(\"RecoveryReasonCode\")) > 0 AND \"RecoverySignaledAtUtc\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_PlaidLinkSession_RecoveryActionAllowed",
                    "\"RecoveryAction\" IS NULL OR \"RecoveryAction\" IN ('none', 'requires_relink', 'requires_update_mode', 'needs_review')");
            });

        modelBuilder.Entity<PlaidLinkSession>()
            .HasIndex(x => x.LinkTokenHash)
            .IsUnique();

        modelBuilder.Entity<PlaidLinkSession>()
            .HasIndex(x => new { x.HouseholdId, x.LinkTokenCreatedAtUtc });

        modelBuilder.Entity<PlaidLinkSessionEvent>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_PlaidLinkSessionEvent_EventTypeRequired",
                "LENGTH(TRIM(\"EventType\")) > 0"));

        modelBuilder.Entity<PlaidLinkSessionEvent>()
            .HasIndex(x => new { x.PlaidLinkSessionId, x.OccurredAtUtc });

        modelBuilder.Entity<PlaidLinkSessionEvent>()
            .HasOne(x => x.PlaidLinkSession)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.PlaidLinkSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaidItemCredential>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PlaidItemCredential_ItemIdRequired",
                    "LENGTH(TRIM(\"ItemId\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidItemCredential_AccessTokenCiphertextRequired",
                    "LENGTH(TRIM(\"AccessTokenCiphertext\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidItemCredential_AccessTokenFingerprintRequired",
                    "LENGTH(TRIM(\"AccessTokenFingerprint\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidItemCredential_RecoveryAudit",
                    "\"RecoveryAction\" IS NULL OR (\"RecoveryReasonCode\" IS NOT NULL AND LENGTH(TRIM(\"RecoveryReasonCode\")) > 0 AND \"RecoverySignaledAtUtc\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_PlaidItemCredential_RecoveryActionAllowed",
                    "\"RecoveryAction\" IS NULL OR \"RecoveryAction\" IN ('none', 'requires_relink', 'requires_update_mode', 'needs_review')");
            });

        modelBuilder.Entity<PlaidItemCredential>()
            .HasIndex(x => new { x.PlaidEnvironment, x.ItemId })
            .IsUnique();

        modelBuilder.Entity<PlaidAccountLink>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PlaidAccountLink_ItemIdRequired",
                    "LENGTH(TRIM(\"ItemId\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidAccountLink_PlaidAccountIdRequired",
                    "LENGTH(TRIM(\"PlaidAccountId\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidAccountLink_UnlinkAudit",
                    "(\"IsActive\" = TRUE AND \"UnlinkedAtUtc\" IS NULL) OR (\"IsActive\" = FALSE AND \"UnlinkedAtUtc\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_PlaidAccountLink_LastSeenAfterLinked",
                    "\"LastSeenAtUtc\" >= \"LinkedAtUtc\"");
            });

        modelBuilder.Entity<PlaidAccountLink>()
            .HasIndex(x => new { x.PlaidEnvironment, x.ItemId, x.PlaidAccountId })
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");

        modelBuilder.Entity<PlaidAccountLink>()
            .HasIndex(x => x.AccountId)
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");

        modelBuilder.Entity<PlaidAccountLink>()
            .HasIndex(x => new { x.PlaidItemCredentialId, x.IsActive, x.LastSeenAtUtc });

        modelBuilder.Entity<PlaidItemCredential>()
            .HasIndex(x => new { x.HouseholdId, x.Status, x.LastRotatedAtUtc });

        modelBuilder.Entity<PlaidItemCredential>()
            .HasMany(x => x.AccountLinks)
            .WithOne(x => x.PlaidItemCredential)
            .HasForeignKey(x => x.PlaidItemCredentialId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PlaidAccountLink>()
            .HasOne(x => x.Account)
            .WithMany(x => x.PlaidAccountLinks)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaidItemSyncState>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PlaidItemSyncState_ItemIdRequired",
                    "LENGTH(TRIM(\"ItemId\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidItemSyncState_CursorRequired",
                    "LENGTH(TRIM(\"Cursor\")) > 0");

                t.HasCheckConstraint(
                    "CK_PlaidItemSyncState_PendingWebhookCountRange",
                    "\"PendingWebhookCount\" >= 0");

                t.HasCheckConstraint(
                    "CK_PlaidItemSyncState_LastSyncErrorAudit",
                    "(\"LastSyncErrorCode\" IS NULL AND \"LastSyncErrorAtUtc\" IS NULL) OR (\"LastSyncErrorCode\" IS NOT NULL AND LENGTH(TRIM(\"LastSyncErrorCode\")) > 0 AND \"LastSyncErrorAtUtc\" IS NOT NULL)");
            });

        modelBuilder.Entity<PlaidItemSyncState>()
            .HasIndex(x => new { x.PlaidEnvironment, x.ItemId })
            .IsUnique();

        modelBuilder.Entity<PlaidItemSyncState>()
            .HasIndex(x => new { x.SyncStatus, x.LastWebhookAtUtc, x.LastSyncedAtUtc });

        modelBuilder.Entity<LiabilityAccount>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_LiabilityAccount_ItemIdRequired",
                    "LENGTH(TRIM(\"ItemId\")) > 0");

                t.HasCheckConstraint(
                    "CK_LiabilityAccount_PlaidAccountIdRequired",
                    "LENGTH(TRIM(\"PlaidAccountId\")) > 0");

                t.HasCheckConstraint(
                    "CK_LiabilityAccount_NameRequired",
                    "LENGTH(TRIM(\"Name\")) > 0");
            });

        modelBuilder.Entity<LiabilityAccount>()
            .HasIndex(x => new { x.PlaidEnvironment, x.ItemId, x.PlaidAccountId })
            .IsUnique();

        modelBuilder.Entity<LiabilityAccount>()
            .HasIndex(x => new { x.HouseholdId, x.IsActive, x.LastSeenAtUtc });

        modelBuilder.Entity<LiabilitySnapshot>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_LiabilitySnapshot_LiabilityTypeRequired",
                    "LENGTH(TRIM(\"LiabilityType\")) > 0");

                t.HasCheckConstraint(
                    "CK_LiabilitySnapshot_SnapshotHashRequired",
                    "LENGTH(TRIM(\"SnapshotHash\")) > 0");

                t.HasCheckConstraint(
                    "CK_LiabilitySnapshot_AprRange",
                    "\"Apr\" IS NULL OR (\"Apr\" >= 0 AND \"Apr\" <= 100)");
            });

        modelBuilder.Entity<LiabilitySnapshot>()
            .HasIndex(x => new { x.LiabilityAccountId, x.SnapshotHash })
            .IsUnique();

        modelBuilder.Entity<LiabilitySnapshot>()
            .HasIndex(x => new { x.LiabilityAccountId, x.CapturedAtUtc });

        modelBuilder.Entity<LiabilitySnapshot>()
            .HasOne(x => x.LiabilityAccount)
            .WithMany(x => x.Snapshots)
            .HasForeignKey(x => x.LiabilityAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InvestmentAccount>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_InvestmentAccount_ItemIdRequired",
                    "LENGTH(TRIM(\"ItemId\")) > 0");

                t.HasCheckConstraint(
                    "CK_InvestmentAccount_PlaidAccountIdRequired",
                    "LENGTH(TRIM(\"PlaidAccountId\")) > 0");

                t.HasCheckConstraint(
                    "CK_InvestmentAccount_NameRequired",
                    "LENGTH(TRIM(\"Name\")) > 0");
            });

        modelBuilder.Entity<InvestmentAccount>()
            .HasIndex(x => new { x.PlaidEnvironment, x.ItemId, x.PlaidAccountId })
            .IsUnique();

        modelBuilder.Entity<InvestmentAccount>()
            .HasIndex(x => new { x.HouseholdId, x.IsActive, x.LastSeenAtUtc });

        modelBuilder.Entity<InvestmentHoldingSnapshot>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_InvestmentHoldingSnapshot_PlaidSecurityIdRequired",
                    "LENGTH(TRIM(\"PlaidSecurityId\")) > 0");

                t.HasCheckConstraint(
                    "CK_InvestmentHoldingSnapshot_SnapshotHashRequired",
                    "LENGTH(TRIM(\"SnapshotHash\")) > 0");
            });

        modelBuilder.Entity<InvestmentHoldingSnapshot>()
            .HasIndex(x => new { x.InvestmentAccountId, x.SnapshotHash })
            .IsUnique();

        modelBuilder.Entity<InvestmentHoldingSnapshot>()
            .HasIndex(x => new { x.InvestmentAccountId, x.CapturedAtUtc });

        modelBuilder.Entity<InvestmentHoldingSnapshot>()
            .HasOne(x => x.InvestmentAccount)
            .WithMany(x => x.Holdings)
            .HasForeignKey(x => x.InvestmentAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InvestmentTransaction>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_InvestmentTransaction_PlaidInvestmentTransactionIdRequired",
                    "LENGTH(TRIM(\"PlaidInvestmentTransactionId\")) > 0");

                t.HasCheckConstraint(
                    "CK_InvestmentTransaction_NameRequired",
                    "LENGTH(TRIM(\"Name\")) > 0");
            });

        modelBuilder.Entity<InvestmentTransaction>()
            .HasIndex(x => new { x.InvestmentAccountId, x.PlaidInvestmentTransactionId })
            .IsUnique();

        modelBuilder.Entity<InvestmentTransaction>()
            .HasIndex(x => new { x.InvestmentAccountId, x.Date });

        modelBuilder.Entity<InvestmentTransaction>()
            .HasOne(x => x.InvestmentAccount)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.InvestmentAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReimbursementProposal>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_ReimbursementProposal_OneRelatedTarget",
                "(\"RelatedTransactionId\" IS NOT NULL AND \"RelatedTransactionSplitId\" IS NULL) OR (\"RelatedTransactionId\" IS NULL AND \"RelatedTransactionSplitId\" IS NOT NULL)"));

        modelBuilder.Entity<ReimbursementProposal>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_ReimbursementProposal_LifecycleOrdinal",
                    "\"LifecycleOrdinal\" >= 1");

                t.HasCheckConstraint(
                    "CK_ReimbursementProposal_RationaleRequired",
                    "LENGTH(TRIM(\"StatusReasonCode\")) > 0 AND LENGTH(TRIM(\"StatusRationale\")) > 0");

                t.HasCheckConstraint(
                    "CK_ReimbursementProposal_ProvenanceRequired",
                    "LENGTH(TRIM(\"ProvenanceSource\")) > 0");

                t.HasCheckConstraint(
                    "CK_ReimbursementProposal_DecisionAuditForFinalStates",
                    "(\"Status\" IN (2, 3) AND \"DecisionedByUserId\" IS NOT NULL AND \"DecisionedAtUtc\" IS NOT NULL) OR (\"Status\" NOT IN (2, 3))");
            });

        modelBuilder.Entity<ReimbursementProposal>()
            .HasIndex(x => new { x.IncomingTransactionId, x.Status });

        modelBuilder.Entity<ReimbursementProposal>()
            .HasIndex(x => new { x.Status, x.CreatedAtUtc });

        modelBuilder.Entity<ReimbursementProposal>()
            .HasIndex(x => new { x.IncomingTransactionId, x.LifecycleGroupId, x.LifecycleOrdinal })
            .IsUnique();

        modelBuilder.Entity<ReimbursementProposal>()
            .HasIndex(x => new { x.LifecycleGroupId, x.CreatedAtUtc });

        modelBuilder.Entity<ReimbursementProposal>()
            .HasOne(x => x.IncomingTransaction)
            .WithMany(x => x.ReimbursementProposals)
            .HasForeignKey(x => x.IncomingTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReimbursementProposal>()
            .HasOne<EnrichedTransaction>()
            .WithMany()
            .HasForeignKey(x => x.RelatedTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReimbursementProposal>()
            .HasOne<TransactionSplit>()
            .WithMany()
            .HasForeignKey(x => x.RelatedTransactionSplitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReimbursementProposal>()
            .HasOne<HouseholdUser>()
            .WithMany()
            .HasForeignKey(x => x.DecisionedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ReimbursementProposal>()
            .HasOne(x => x.SupersedesProposal)
            .WithMany(x => x.SupersededByProposals)
            .HasForeignKey(x => x.SupersedesProposalId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TransactionClassificationOutcome>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_TransactionClassificationOutcome_FinalConfidenceRange",
                    "\"FinalConfidence\" >= 0 AND \"FinalConfidence\" <= 1");

                t.HasCheckConstraint(
                    "CK_TransactionClassificationOutcome_DecisionReviewRouting",
                    "(\"Decision\" = 2 AND \"ReviewStatus\" = 1) OR (\"Decision\" = 1 AND \"ReviewStatus\" <> 1)");
            });

        modelBuilder.Entity<TransactionClassificationOutcome>()
            .HasIndex(x => new { x.TransactionId, x.CreatedAtUtc });

        modelBuilder.Entity<TransactionClassificationOutcome>()
            .HasIndex(x => new { x.Decision, x.ReviewStatus, x.CreatedAtUtc });

        modelBuilder.Entity<TransactionClassificationOutcome>()
            .HasOne(x => x.Transaction)
            .WithMany(x => x.ClassificationOutcomes)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TransactionClassificationOutcome>()
            .HasOne(x => x.ProposedSubcategory)
            .WithMany(x => x.ClassificationOutcomeProposals)
            .HasForeignKey(x => x.ProposedSubcategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ClassificationStageOutput>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_ClassificationStageOutput_ConfidenceRange",
                    "\"Confidence\" >= 0 AND \"Confidence\" <= 1");

                t.HasCheckConstraint(
                    "CK_ClassificationStageOutput_StageOrderRange",
                    "\"StageOrder\" >= 1 AND \"StageOrder\" <= 3");
            });

        modelBuilder.Entity<ClassificationStageOutput>()
            .HasIndex(x => new { x.OutcomeId, x.Stage })
            .IsUnique();

        modelBuilder.Entity<ClassificationStageOutput>()
            .HasOne(x => x.Outcome)
            .WithMany(x => x.StageOutputs)
            .HasForeignKey(x => x.OutcomeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassificationStageOutput>()
            .HasOne(x => x.ProposedSubcategory)
            .WithMany(x => x.ClassificationStageProposals)
            .HasForeignKey(x => x.ProposedSubcategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentRun>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_AgentRun_CorrelationIdRequired",
                    "LENGTH(TRIM(\"CorrelationId\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentRun_WorkflowNameRequired",
                    "LENGTH(TRIM(\"WorkflowName\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentRun_TriggerSourceRequired",
                    "LENGTH(TRIM(\"TriggerSource\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentRun_PolicyVersionRequired",
                    "LENGTH(TRIM(\"PolicyVersion\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentRun_StatusRange",
                    "\"Status\" IN (1, 2, 3, 4, 5, 6)");

                t.HasCheckConstraint(
                    "CK_AgentRun_LastModifiedAfterCreated",
                    "\"LastModifiedAtUtc\" >= \"CreatedAtUtc\"");

                t.HasCheckConstraint(
                    "CK_AgentRun_CompletedAfterCreated",
                    "\"CompletedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"CreatedAtUtc\"");

                t.HasCheckConstraint(
                    "CK_AgentRun_TerminalCompletionAudit",
                    "((\"Status\" IN (1, 2)) AND \"CompletedAtUtc\" IS NULL) OR ((\"Status\" IN (3, 4, 5, 6)) AND \"CompletedAtUtc\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_AgentRun_FailureAuditForEscalatedStates",
                    "((\"Status\" IN (4, 5, 6)) AND \"FailureCode\" IS NOT NULL AND LENGTH(TRIM(\"FailureCode\")) > 0 AND \"FailureRationale\" IS NOT NULL AND LENGTH(TRIM(\"FailureRationale\")) > 0) OR (\"Status\" NOT IN (4, 5, 6))");
            });

        modelBuilder.Entity<AgentRun>()
            .HasIndex(x => new { x.CorrelationId, x.CreatedAtUtc });

        modelBuilder.Entity<AgentRun>()
            .HasIndex(x => new { x.WorkflowName, x.TriggerSource, x.CreatedAtUtc });

        modelBuilder.Entity<AgentRun>()
            .HasIndex(x => new { x.HouseholdId, x.Status, x.CreatedAtUtc });

        modelBuilder.Entity<AgentRunStage>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_AgentRunStage_StageNameRequired",
                    "LENGTH(TRIM(\"StageName\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_ExecutorRequired",
                    "LENGTH(TRIM(\"Executor\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_StatusRange",
                    "\"Status\" IN (1, 2, 3, 4, 5, 6)");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_StageOrderRange",
                    "\"StageOrder\" >= 1 AND \"StageOrder\" <= 64");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_ConfidenceRange",
                    "\"Confidence\" IS NULL OR (\"Confidence\" >= 0 AND \"Confidence\" <= 1)");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_LastModifiedAfterCreated",
                    "\"LastModifiedAtUtc\" >= \"CreatedAtUtc\"");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_CompletedAfterCreated",
                    "\"CompletedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"CreatedAtUtc\"");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_TerminalCompletionAudit",
                    "((\"Status\" IN (1, 2)) AND \"CompletedAtUtc\" IS NULL) OR ((\"Status\" IN (3, 4, 5, 6)) AND \"CompletedAtUtc\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_AgentRunStage_TerminalOutcomeRequired",
                    "(\"Status\" IN (1, 2)) OR (\"OutcomeCode\" IS NOT NULL AND LENGTH(TRIM(\"OutcomeCode\")) > 0 AND \"OutcomeRationale\" IS NOT NULL AND LENGTH(TRIM(\"OutcomeRationale\")) > 0)");
            });

        modelBuilder.Entity<AgentRunStage>()
            .HasIndex(x => new { x.AgentRunId, x.StageOrder })
            .IsUnique();

        modelBuilder.Entity<AgentRunStage>()
            .HasIndex(x => new { x.AgentRunId, x.Status, x.StageOrder });

        modelBuilder.Entity<AgentRunStage>()
            .HasIndex(x => new { x.Status, x.CreatedAtUtc });

        modelBuilder.Entity<AgentSignal>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_AgentSignal_SignalCodeRequired",
                    "LENGTH(TRIM(\"SignalCode\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentSignal_SummaryRequired",
                    "LENGTH(TRIM(\"Summary\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentSignal_SeverityRange",
                    "\"Severity\" IN (1, 2, 3, 4)");

                t.HasCheckConstraint(
                    "CK_AgentSignal_ResolutionAudit",
                    "(\"IsResolved\" = TRUE AND \"ResolvedAtUtc\" IS NOT NULL) OR (\"IsResolved\" = FALSE AND \"ResolvedAtUtc\" IS NULL)");

                t.HasCheckConstraint(
                    "CK_AgentSignal_HumanReviewRequiredForHighSeverity",
                    "(\"Severity\" IN (1, 2)) OR \"RequiresHumanReview\" = TRUE");
            });

        modelBuilder.Entity<AgentSignal>()
            .Property(x => x.RequiresHumanReview)
            .HasDefaultValue(true);

        modelBuilder.Entity<AgentSignal>()
            .HasIndex(x => new { x.AgentRunId, x.RaisedAtUtc });

        modelBuilder.Entity<AgentSignal>()
            .HasIndex(x => new { x.RequiresHumanReview, x.IsResolved, x.RaisedAtUtc });

        modelBuilder.Entity<AgentSignal>()
            .HasIndex(x => new { x.AgentRunStageId, x.RaisedAtUtc });

        modelBuilder.Entity<AgentDecisionAudit>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_DecisionTypeRequired",
                    "LENGTH(TRIM(\"DecisionType\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_ReasonCodeRequired",
                    "LENGTH(TRIM(\"ReasonCode\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_RationaleRequired",
                    "LENGTH(TRIM(\"Rationale\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_PolicyVersionRequired",
                    "LENGTH(TRIM(\"PolicyVersion\")) > 0");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_OutcomeRange",
                    "\"Outcome\" IN (1, 2, 3, 4)");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_ConfidenceRange",
                    "\"Confidence\" IS NULL OR (\"Confidence\" >= 0 AND \"Confidence\" <= 1)");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_FailClosedNeedsReview",
                    "(\"Outcome\" = 2 AND \"ReviewStatus\" = 1) OR (\"Outcome\" <> 2 AND \"ReviewStatus\" <> 1)");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_ReviewedAudit",
                    "(\"ReviewedByUserId\" IS NULL AND \"ReviewedAtUtc\" IS NULL) OR (\"ReviewedByUserId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_AgentDecisionAudit_ReviewedAfterDecision",
                    "\"ReviewedAtUtc\" IS NULL OR \"ReviewedAtUtc\" >= \"DecidedAtUtc\"");
            });

        modelBuilder.Entity<AgentDecisionAudit>()
            .HasIndex(x => new { x.AgentRunId, x.DecidedAtUtc });

        modelBuilder.Entity<AgentDecisionAudit>()
            .HasIndex(x => new { x.Outcome, x.ReviewStatus, x.DecidedAtUtc });

        modelBuilder.Entity<AgentDecisionAudit>()
            .HasIndex(x => new { x.AgentRunStageId, x.DecidedAtUtc });

        modelBuilder.Entity<AgentDecisionAudit>()
            .HasIndex(x => new { x.ReviewedByUserId, x.ReviewedAtUtc });

        modelBuilder.Entity<IdempotencyKey>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_IdempotencyKey_ScopeRequired",
                    "LENGTH(TRIM(\"Scope\")) > 0");

                t.HasCheckConstraint(
                    "CK_IdempotencyKey_KeyValueRequired",
                    "LENGTH(TRIM(\"KeyValue\")) > 0");

                t.HasCheckConstraint(
                    "CK_IdempotencyKey_RequestHashRequired",
                    "LENGTH(TRIM(\"RequestHash\")) > 0");

                t.HasCheckConstraint(
                    "CK_IdempotencyKey_StatusRange",
                    "\"Status\" IN (1, 2, 3, 4)");

                t.HasCheckConstraint(
                    "CK_IdempotencyKey_ExpiresAfterCreated",
                    "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");

                t.HasCheckConstraint(
                    "CK_IdempotencyKey_FinalizationAudit",
                    "((\"Status\" = 1) AND \"FinalizedAtUtc\" IS NULL) OR ((\"Status\" IN (2, 3, 4)) AND \"FinalizedAtUtc\" IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_IdempotencyKey_ResolutionAudit",
                    "(\"ResolutionCode\" IS NULL AND \"ResolutionRationale\" IS NULL) OR (\"ResolutionCode\" IS NOT NULL AND LENGTH(TRIM(\"ResolutionCode\")) > 0 AND \"ResolutionRationale\" IS NOT NULL AND LENGTH(TRIM(\"ResolutionRationale\")) > 0)");

                t.HasCheckConstraint(
                    "CK_IdempotencyKey_FinalizedAfterCreated",
                    "\"FinalizedAtUtc\" IS NULL OR \"FinalizedAtUtc\" >= \"CreatedAtUtc\"");
            });

        modelBuilder.Entity<IdempotencyKey>()
            .HasIndex(x => new { x.Scope, x.KeyValue })
            .IsUnique();

        modelBuilder.Entity<IdempotencyKey>()
            .HasIndex(x => new { x.Status, x.ExpiresAtUtc });

        modelBuilder.Entity<IdempotencyKey>()
            .HasIndex(x => new { x.AgentRunId, x.CreatedAtUtc });

        modelBuilder.Entity<Household>()
            .HasMany(x => x.Users)
            .WithOne(x => x.Household)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MosaicUser>()
            .HasMany(x => x.HouseholdMemberships)
            .WithOne(x => x.MosaicUser)
            .HasForeignKey(x => x.MosaicUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Household>()
            .HasMany(x => x.Accounts)
            .WithOne(x => x.Household)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Household>()
            .HasMany(x => x.RecurringItems)
            .WithOne(x => x.Household)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Household>()
            .HasMany<AgentRun>()
            .WithOne(x => x.Household)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Category>()
            .HasMany(x => x.Subcategories)
            .WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(x => x.Transactions)
            .WithOne(x => x.Account)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(x => x.MemberAccessGrants)
            .WithOne(x => x.Account)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HouseholdUser>()
            .HasMany(x => x.AccountAccessGrants)
            .WithOne(x => x.HouseholdUser)
            .HasForeignKey(x => x.HouseholdUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecurringItem>()
            .HasMany(x => x.Transactions)
            .WithOne(x => x.RecurringItem)
            .HasForeignKey(x => x.RecurringItemId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Subcategory>()
            .HasMany(x => x.Transactions)
            .WithOne(x => x.Subcategory)
            .HasForeignKey(x => x.SubcategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<HouseholdUser>()
            .HasMany(x => x.NeedsReviewTransactions)
            .WithOne(x => x.NeedsReviewByUser)
            .HasForeignKey(x => x.NeedsReviewByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<EnrichedTransaction>()
            .HasMany(x => x.Splits)
            .WithOne(x => x.ParentTransaction)
            .HasForeignKey(x => x.ParentTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Subcategory>()
            .HasMany(x => x.Splits)
            .WithOne(x => x.Subcategory)
            .HasForeignKey(x => x.SubcategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentRun>()
            .HasMany(x => x.Stages)
            .WithOne(x => x.AgentRun)
            .HasForeignKey(x => x.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AgentRun>()
            .HasMany(x => x.Signals)
            .WithOne(x => x.AgentRun)
            .HasForeignKey(x => x.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AgentRun>()
            .HasMany(x => x.DecisionAudits)
            .WithOne(x => x.AgentRun)
            .HasForeignKey(x => x.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AgentRun>()
            .HasMany(x => x.IdempotencyKeys)
            .WithOne(x => x.AgentRun)
            .HasForeignKey(x => x.AgentRunId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentRunStage>()
            .HasMany(x => x.Signals)
            .WithOne(x => x.AgentRunStage)
            .HasForeignKey(x => x.AgentRunStageId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentRunStage>()
            .HasMany(x => x.DecisionAudits)
            .WithOne(x => x.AgentRunStage)
            .HasForeignKey(x => x.AgentRunStageId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentDecisionAudit>()
            .HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

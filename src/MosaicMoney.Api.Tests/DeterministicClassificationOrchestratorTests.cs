using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class DeterministicClassificationOrchestratorTests
{
    [Fact]
    public async Task ClassifyAndPersistAsync_HighConfidenceRule_PersistsCategorizedOutcomeAndUpdatesTransaction()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "Austin Energy bill payment", -120.00m);
        var semanticStub = new StubSemanticRetrievalService();
        var mafStub = new StubMafFallbackGraphService();

        var orchestrator = CreateOrchestrator(dbContext, semanticStub, mafFallbackGraphService: mafStub);
        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.Categorized, result!.Outcome.Decision);
        Assert.Equal(ClassificationAmbiguityReasonCodes.DeterministicAccepted, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.None, result.TransactionReviewStatus);
        Assert.Equal(seeded.EnergySubcategoryId, result.TransactionSubcategoryId);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        var stageOutput = Assert.Single(persistedOutcome.StageOutputs);
        Assert.Equal(ClassificationStage.Deterministic, stageOutput.Stage);
        Assert.Equal(1, stageOutput.StageOrder);
        Assert.False(stageOutput.EscalatedToNextStage);
        Assert.Contains(
            $"Gate decision {ClassificationAmbiguityReasonCodes.DeterministicAccepted}",
            stageOutput.Rationale,
            StringComparison.Ordinal);
        Assert.Equal(0, semanticStub.CallCount);
        Assert.Equal(0, mafStub.CallCount);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_LowConfidenceRule_RoutesToNeedsReviewWithReasonCodeAndSemanticEvidence()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "energy", -75.00m);
        var semanticStub = new StubSemanticRetrievalService
        {
            ResultToReturn = new SemanticRetrievalResult(
                Succeeded: true,
                StatusCode: SemanticRetrievalStatusCodes.Ok,
                StatusMessage: "Semantic candidates resolved successfully.",
                Candidates:
                [
                    new SemanticRetrievalCandidate(
                        seeded.EnergySubcategoryId,
                        0.9150m,
                        Guid.NewGuid(),
                        2,
                        "postgresql.pgvector.cosine_distance",
                        "seed-source",
                        "{\"NormalizedScore\":0.9150}")
                ])
        };
            var mafStub = new StubMafFallbackGraphService();

            var orchestrator = CreateOrchestrator(dbContext, semanticStub, mafFallbackGraphService: mafStub);
        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId, seeded.ReviewerId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(ClassificationAmbiguityReasonCodes.LowConfidence, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.LowConfidence, result.TransactionReviewReason);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == seeded.TransactionId);
        Assert.Equal(seeded.ReviewerId, transaction.NeedsReviewByUserId);
        Assert.Null(transaction.SubcategoryId);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        Assert.Equal(2, persistedOutcome.StageOutputs.Count);
        var deterministicOutput = persistedOutcome.StageOutputs.Single(x => x.Stage == ClassificationStage.Deterministic);
        Assert.Contains(
            $"Gate decision {ClassificationAmbiguityReasonCodes.LowConfidence}",
            deterministicOutput.Rationale,
            StringComparison.Ordinal);

        var semanticOutput = persistedOutcome.StageOutputs.Single(x => x.Stage == ClassificationStage.Semantic);
        Assert.Equal(2, semanticOutput.StageOrder);
        Assert.Equal(SemanticRetrievalStatusCodes.Ok, semanticOutput.RationaleCode);
        Assert.Equal(seeded.EnergySubcategoryId, semanticOutput.ProposedSubcategoryId);
        Assert.Contains(
            $"Fusion decision {ClassificationAmbiguityReasonCodes.LowConfidence}",
            semanticOutput.Rationale,
            StringComparison.Ordinal);
        Assert.Equal(1, semanticStub.CallCount);
        Assert.Equal(0, mafStub.CallCount);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_ConflictingRules_RoutesToNeedsReviewFailClosed()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "HEB purchase", -35.00m);
        var semanticStub = new StubSemanticRetrievalService
        {
            ResultToReturn = new SemanticRetrievalResult(
                Succeeded: true,
                StatusCode: SemanticRetrievalStatusCodes.NoCandidates,
                StatusMessage: "No semantic candidates met the configured score threshold.",
                Candidates: [])
        };
            var mafStub = new StubMafFallbackGraphService();

            var orchestrator = CreateOrchestrator(dbContext, semanticStub, mafFallbackGraphService: mafStub);
        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(ClassificationAmbiguityReasonCodes.ConflictingDeterministicRules, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.ConflictingDeterministicRules, result.TransactionReviewReason);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == seeded.TransactionId);
        Assert.Equal(TransactionReviewStatus.NeedsReview, transaction.ReviewStatus);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        Assert.Equal(2, persistedOutcome.StageOutputs.Count);
        var semanticOutput = persistedOutcome.StageOutputs.Single(x => x.Stage == ClassificationStage.Semantic);
        Assert.Equal(SemanticRetrievalStatusCodes.NoCandidates, semanticOutput.RationaleCode);
        Assert.Null(semanticOutput.ProposedSubcategoryId);
        Assert.Equal(1, semanticStub.CallCount);
        Assert.Equal(0, mafStub.CallCount);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_EligibleMafFallback_PersistsProposalAsStageOutputOnly()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "unknown merchant", -42.15m);

        var semanticStub = new StubSemanticRetrievalService
        {
            ResultToReturn = new SemanticRetrievalResult(
                Succeeded: true,
                StatusCode: SemanticRetrievalStatusCodes.NoCandidates,
                StatusMessage: "No semantic candidates met the configured score threshold.",
                Candidates: [])
        };

        var mafEligibilityStub = new StubMafFallbackEligibilityGate { IsEligible = true };
        var mafStub = new StubMafFallbackGraphService
        {
            ResultToReturn = new MafFallbackGraphResult(
                Succeeded: true,
                StatusCode: MafFallbackGraphStatusCodes.Ok,
                StatusMessage: "MAF fallback returned schema-valid proposals.",
                Proposals:
                [
                    new MafFallbackProposal(
                        seeded.EnergySubcategoryId,
                        0.8840m,
                        "maf_graph_top_proposal",
                        "MAF fallback identified the top proposal after deterministic and semantic insufficiency.",
                        "MAF fallback suggested a review candidate with confidence 0.8840.",
                        "draft_message",
                        "Draft reminder for user approval.")
                ],
                MessagingSendDenied: false,
                MessagingSendDeniedCount: 0,
                MessagingSendDeniedActions: null)
        };

        var orchestrator = CreateOrchestrator(
            dbContext,
            semanticStub,
            mafEligibilityGate: mafEligibilityStub,
            mafFallbackGraphService: mafStub);

        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId, seeded.ReviewerId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal("maf_graph_top_proposal", result.Outcome.DecisionReasonCode);
        Assert.Equal(0.8840m, result.Outcome.FinalConfidence);
        Assert.Contains("MAF fallback identified the top proposal", result.Outcome.DecisionRationale, StringComparison.Ordinal);
        Assert.Null(result.TransactionSubcategoryId);
        Assert.Equal(1, mafStub.CallCount);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        Assert.Equal(3, persistedOutcome.StageOutputs.Count);
        Assert.Equal(seeded.EnergySubcategoryId, persistedOutcome.ProposedSubcategoryId);

        var mafStage = persistedOutcome.StageOutputs.Single(x => x.Stage == ClassificationStage.MafFallback);
        Assert.Equal(3, mafStage.StageOrder);
        Assert.Equal(seeded.EnergySubcategoryId, mafStage.ProposedSubcategoryId);
        Assert.Equal(0.8840m, mafStage.Confidence);
        Assert.Equal("maf_graph_top_proposal", mafStage.RationaleCode);
        Assert.False(mafStage.EscalatedToNextStage);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == seeded.TransactionId);
        Assert.Equal(TransactionReviewStatus.NeedsReview, transaction.ReviewStatus);
        Assert.Null(transaction.SubcategoryId);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_MafFallbackTimeout_FailsClosed()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "unknown merchant", -39.01m);

        var semanticStub = new StubSemanticRetrievalService
        {
            ResultToReturn = new SemanticRetrievalResult(
                Succeeded: true,
                StatusCode: SemanticRetrievalStatusCodes.NoCandidates,
                StatusMessage: "No semantic candidates met the configured score threshold.",
                Candidates: [])
        };

        var mafEligibilityStub = new StubMafFallbackEligibilityGate { IsEligible = true };
        var mafStub = new StubMafFallbackGraphService
        {
            ResultToReturn = new MafFallbackGraphResult(
                Succeeded: false,
                StatusCode: MafFallbackGraphStatusCodes.Timeout,
                StatusMessage: "MAF fallback timed out after 8s.",
                Proposals: [],
                MessagingSendDenied: false,
                MessagingSendDeniedCount: 0,
                MessagingSendDeniedActions: null)
        };

        var orchestrator = CreateOrchestrator(
            dbContext,
            semanticStub,
            mafEligibilityGate: mafEligibilityStub,
            mafFallbackGraphService: mafStub);

        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal(MafFallbackGraphStatusCodes.Timeout, result.Outcome.DecisionReasonCode);
        Assert.Contains("timed out", result.Outcome.DecisionRationale, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, mafStub.CallCount);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        Assert.Null(persistedOutcome.ProposedSubcategoryId);

        var mafStage = persistedOutcome.StageOutputs.Single(x => x.Stage == ClassificationStage.MafFallback);
        Assert.Equal(MafFallbackGraphStatusCodes.Timeout, mafStage.RationaleCode);
        Assert.Equal(0m, mafStage.Confidence);
        Assert.Null(mafStage.ProposedSubcategoryId);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == seeded.TransactionId);
        Assert.Null(transaction.SubcategoryId);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_MafFallbackDeniedSendAction_PersistsGuardrailAuditRationale()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "unknown merchant", -51.33m);

        var semanticStub = new StubSemanticRetrievalService
        {
            ResultToReturn = new SemanticRetrievalResult(
                Succeeded: true,
                StatusCode: SemanticRetrievalStatusCodes.NoCandidates,
                StatusMessage: "No semantic candidates met the configured score threshold.",
                Candidates: [])
        };

        var mafEligibilityStub = new StubMafFallbackEligibilityGate { IsEligible = true };
        var mafStub = new StubMafFallbackGraphService
        {
            ResultToReturn = new MafFallbackGraphResult(
                Succeeded: true,
                StatusCode: MafFallbackGraphStatusCodes.Ok,
                StatusMessage: "MAF fallback returned schema-valid proposals.",
                Proposals:
                [
                    new MafFallbackProposal(
                        seeded.EnergySubcategoryId,
                        0.9030m,
                        "maf_graph_top_proposal",
                        "MAF fallback identified a likely category for review.",
                        "Concise review summary.",
                        "draft_message",
                        "Draft text for human review only.")
                ],
                MessagingSendDenied: true,
                MessagingSendDeniedCount: 1,
                MessagingSendDeniedActions: "send_message")
        };

        var orchestrator = CreateOrchestrator(
            dbContext,
            semanticStub,
            mafEligibilityGate: mafEligibilityStub,
            mafFallbackGraphService: mafStub);

        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId, seeded.ReviewerId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(MafFallbackGraphStatusCodes.ExternalMessagingSendDenied, result.Outcome.DecisionReasonCode);
        Assert.Contains("Guardrail denied 1 external send action", result.Outcome.DecisionRationale, StringComparison.Ordinal);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        var mafStage = persistedOutcome.StageOutputs.Single(x => x.Stage == ClassificationStage.MafFallback);
        Assert.Equal(MafFallbackGraphStatusCodes.ExternalMessagingSendDenied, mafStage.RationaleCode);
        Assert.Contains("Guardrail denied 1 external send action", mafStage.Rationale, StringComparison.Ordinal);
        Assert.Contains("send_message", mafStage.Rationale, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_SpecialistIncomeLane_RoutesFailClosedWithoutSemanticOrMaf()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "Payroll direct deposit", 1850.45m);
        var semanticStub = new StubSemanticRetrievalService();
        var mafEligibilityStub = new StubMafFallbackEligibilityGate { IsEligible = true };
        var mafStub = new StubMafFallbackGraphService();

        var specialistOptions = new ClassificationSpecialistRegistryOptions
        {
            EnableRoutingPolicy = true,
        };

        var orchestrator = CreateOrchestrator(
            dbContext,
            semanticStub,
            mafEligibilityGate: mafEligibilityStub,
            mafFallbackGraphService: mafStub,
            specialistRoutingPolicy: CreateSpecialistRoutingPolicy(specialistOptions));

        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId, seeded.ReviewerId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.SpecialistEscalationRequired, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.SpecialistEscalationRequired, result.TransactionReviewReason);
        Assert.Equal(0, semanticStub.CallCount);
        Assert.Equal(0, mafStub.CallCount);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        var deterministicOutput = Assert.Single(persistedOutcome.StageOutputs);
        Assert.Equal(ClassificationStage.Deterministic, deterministicOutput.Stage);
        Assert.Contains(
            ClassificationSpecialistRoutingReasonCodes.SpecialistEscalationRequired,
            deterministicOutput.Rationale,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_SpecialistDisabledLane_RoutesFailClosed()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "Transfer to savings", -52.10m);
        var semanticStub = new StubSemanticRetrievalService();
        var mafEligibilityStub = new StubMafFallbackEligibilityGate { IsEligible = true };
        var mafStub = new StubMafFallbackGraphService();

        var specialistOptions = new ClassificationSpecialistRegistryOptions
        {
            EnableRoutingPolicy = true,
        };

        specialistOptions.Specialists[ClassificationSpecialistKeys.Transfer] = new ClassificationSpecialistRegistrationOptions
        {
            SpecialistId = ClassificationSpecialistKeys.Transfer,
            Enabled = false,
            AllowSemanticStage = false,
            AllowMafFallbackStage = false,
        };

        var orchestrator = CreateOrchestrator(
            dbContext,
            semanticStub,
            mafEligibilityGate: mafEligibilityStub,
            mafFallbackGraphService: mafStub,
            specialistRoutingPolicy: CreateSpecialistRoutingPolicy(specialistOptions));

        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId, seeded.ReviewerId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.SpecialistDisabled, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.SpecialistDisabled, result.TransactionReviewReason);
        Assert.Equal(0, semanticStub.CallCount);
        Assert.Equal(0, mafStub.CallCount);
    }

    private static DeterministicClassificationOrchestrator CreateOrchestrator(
        MosaicMoneyDbContext dbContext,
        IPostgresSemanticRetrievalService semanticRetrievalService,
        IMafFallbackEligibilityGate? mafEligibilityGate = null,
        IMafFallbackGraphService? mafFallbackGraphService = null,
        IClassificationSpecialistRoutingPolicy? specialistRoutingPolicy = null)
    {
        return new DeterministicClassificationOrchestrator(
            dbContext,
            new DeterministicClassificationEngine(),
            new ClassificationAmbiguityPolicyGate(),
            new ClassificationConfidenceFusionPolicy(),
            specialistRoutingPolicy ?? CreateSpecialistRoutingPolicy(new ClassificationSpecialistRegistryOptions()),
            semanticRetrievalService,
            mafEligibilityGate ?? new StubMafFallbackEligibilityGate(),
            mafFallbackGraphService ?? new StubMafFallbackGraphService());
    }

    private static IClassificationSpecialistRoutingPolicy CreateSpecialistRoutingPolicy(
        ClassificationSpecialistRegistryOptions options)
    {
        return new ClassificationSpecialistRoutingPolicy(
            new ClassificationSpecialistRegistry(Options.Create(options)));
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-classification-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<(Guid TransactionId, Guid EnergySubcategoryId, Guid ReviewerId)> SeedLedgerDataAsync(
        MosaicMoneyDbContext dbContext,
        string transactionDescription,
        decimal amount)
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var household = new Household
        {
            Id = householdId,
            Name = "Classification Test Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var reviewer = new HouseholdUser
        {
            Id = reviewerId,
            HouseholdId = householdId,
            DisplayName = "Reviewer",
            ExternalUserKey = "reviewer-1",
        };

        var account = new Account
        {
            Id = accountId,
            HouseholdId = householdId,
            Name = "Primary Account",
            InstitutionName = "Test Institution",
            ExternalAccountKey = "acct-1",
            IsActive = true,
        };

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Expenses",
            DisplayOrder = 1,
            IsSystem = false,
        };

        var energySubcategory = new Subcategory
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Austin Energy",
            IsBusinessExpense = false,
        };

        var groceriesSubcategory = new Subcategory
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "HEB Grocery",
            IsBusinessExpense = false,
        };

        var fuelSubcategory = new Subcategory
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "HEB Fuel",
            IsBusinessExpense = false,
        };

        var transaction = new EnrichedTransaction
        {
            Id = transactionId,
            AccountId = accountId,
            Description = transactionDescription,
            Amount = amount,
            TransactionDate = new DateOnly(2026, 2, 23),
            ReviewStatus = TransactionReviewStatus.None,
            CreatedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow,
        };

        dbContext.Households.Add(household);
        dbContext.HouseholdUsers.Add(reviewer);
        dbContext.Accounts.Add(account);
        dbContext.Categories.Add(category);
        dbContext.Subcategories.AddRange(energySubcategory, groceriesSubcategory, fuelSubcategory);
        dbContext.EnrichedTransactions.Add(transaction);

        await dbContext.SaveChangesAsync();

        return (transactionId, energySubcategory.Id, reviewerId);
    }

    private sealed class StubSemanticRetrievalService : IPostgresSemanticRetrievalService
    {
        public int CallCount { get; private set; }

        public SemanticRetrievalResult ResultToReturn { get; set; } = new(
            Succeeded: true,
            StatusCode: SemanticRetrievalStatusCodes.NoCandidates,
            StatusMessage: "No semantic candidates met the configured score threshold.",
            Candidates: []);

        public Task<SemanticRetrievalResult> RetrieveCandidatesAsync(
            Guid transactionId,
            SemanticRetrievalRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class StubMafFallbackEligibilityGate : IMafFallbackEligibilityGate
    {
        public bool IsEligible { get; set; }

        public MafFallbackEligibilityDecision Evaluate(
            ClassificationAmbiguityDecision ambiguityDecision,
            ClassificationConfidenceFusionDecision fusionDecision,
            bool semanticStageAttempted)
        {
            if (IsEligible)
            {
                return new MafFallbackEligibilityDecision(
                    true,
                    MafFallbackEligibilityReasonCodes.EligibleAfterSemanticInsufficiency,
                    "Eligible for test execution.");
            }

            return new MafFallbackEligibilityDecision(
                false,
                MafFallbackEligibilityReasonCodes.IneligibleFinalizedBeforeFallback,
                "Ineligible for test execution.");
        }
    }

    private sealed class StubMafFallbackGraphService : IMafFallbackGraphService
    {
        public int CallCount { get; private set; }

        public MafFallbackGraphResult ResultToReturn { get; set; } = new(
            Succeeded: true,
            StatusCode: MafFallbackGraphStatusCodes.NoProposals,
            StatusMessage: "MAF fallback did not return any schema-valid proposals above threshold.",
            Proposals: [],
            MessagingSendDenied: false,
            MessagingSendDeniedCount: 0,
            MessagingSendDeniedActions: null);

        public Task<MafFallbackGraphResult> ExecuteAsync(
            MafFallbackGraphRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ResultToReturn);
        }
    }
}

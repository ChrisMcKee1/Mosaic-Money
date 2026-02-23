using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class MafFallbackGraphServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Disabled_ReturnsSafeFallbackWithoutExecutorCall()
    {
        var executor = new StubMafFallbackGraphExecutor(_ => Task.FromResult("{\"proposals\":[]}"));
        var service = CreateService(
            executor,
            new MafFallbackGraphOptions
            {
                Enabled = false,
                TimeoutSeconds = 8,
                MaxProposals = 3,
                MinimumProposalConfidence = 0.7m,
            });

        var result = await service.ExecuteAsync(BuildRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(MafFallbackGraphStatusCodes.Disabled, result.StatusCode);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSchema_ReturnsBoundedValidatedProposals()
    {
        var allowedA = Guid.NewGuid();
        var allowedB = Guid.NewGuid();
        var disallowed = Guid.NewGuid();

        var payload = $$"""
        {
          "proposals": [
            {
              "proposedSubcategoryId": "{{allowedA}}",
              "confidence": 0.9123,
              "rationaleCode": "maf_candidate_a",
              "rationale": "Candidate A rationale.",
              "agentNoteSummary": "Summary A"
            },
            {
              "proposedSubcategoryId": "{{allowedB}}",
              "confidence": 0.8700,
              "rationaleCode": "maf_candidate_b",
              "rationale": "Candidate B rationale.",
              "agentNoteSummary": "Summary B"
            },
            {
              "proposedSubcategoryId": "{{disallowed}}",
              "confidence": 0.9900,
              "rationaleCode": "maf_candidate_disallowed",
              "rationale": "Disallowed subcategory should be dropped.",
              "agentNoteSummary": "Drop"
            },
            {
              "proposedSubcategoryId": "{{allowedB}}",
              "confidence": 0.6500,
              "rationaleCode": "maf_candidate_b_low",
              "rationale": "Below threshold should be dropped.",
              "agentNoteSummary": "Drop"
            }
          ]
        }
        """;

        var executor = new StubMafFallbackGraphExecutor(_ => Task.FromResult(payload));
        var service = CreateService(
            executor,
            new MafFallbackGraphOptions
            {
                Enabled = true,
                TimeoutSeconds = 8,
                MaxProposals = 2,
                MinimumProposalConfidence = 0.7m,
            });

        var result = await service.ExecuteAsync(BuildRequest(allowedA, allowedB));

        Assert.True(result.Succeeded);
        Assert.Equal(MafFallbackGraphStatusCodes.Ok, result.StatusCode);
        Assert.Equal(2, result.Proposals.Count);
        Assert.Equal(allowedA, result.Proposals[0].ProposedSubcategoryId);
        Assert.Equal(0.9123m, result.Proposals[0].Confidence);
        Assert.Equal(allowedB, result.Proposals[1].ProposedSubcategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSchema_ReturnsSchemaValidationFailure()
    {
        var executor = new StubMafFallbackGraphExecutor(_ => Task.FromResult("{\"proposals\":{\"unexpected\":true}}"));
        var service = CreateService(
            executor,
            new MafFallbackGraphOptions
            {
                Enabled = true,
                TimeoutSeconds = 8,
                MaxProposals = 3,
                MinimumProposalConfidence = 0.7m,
            });

        var result = await service.ExecuteAsync(BuildRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(MafFallbackGraphStatusCodes.SchemaValidationFailed, result.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ReturnsSafeFailure()
    {
        var executor = new StubMafFallbackGraphExecutor(async cancellationToken =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            return "{\"proposals\":[]}";
        });

        var service = CreateService(
            executor,
            new MafFallbackGraphOptions
            {
                Enabled = true,
                TimeoutSeconds = 1,
                MaxProposals = 3,
                MinimumProposalConfidence = 0.7m,
            });

        var result = await service.ExecuteAsync(BuildRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(MafFallbackGraphStatusCodes.Timeout, result.StatusCode);
    }

    private static MafFallbackGraphService CreateService(
        IMafFallbackGraphExecutor executor,
        MafFallbackGraphOptions options)
    {
        return new MafFallbackGraphService(
            executor,
            Options.Create(options),
            NullLogger<MafFallbackGraphService>.Instance);
    }

    private static MafFallbackGraphRequest BuildRequest(params Guid[] allowedSubcategoryIds)
    {
        var subcategoryIds = allowedSubcategoryIds.Length == 0
            ? [Guid.NewGuid()]
            : allowedSubcategoryIds;

        var subcategories = subcategoryIds
            .Select((id, index) => new DeterministicClassificationSubcategory(id, $"subcategory-{index + 1}"))
            .ToList();

        return new MafFallbackGraphRequest(
            TransactionId: Guid.NewGuid(),
            Description: "Unknown merchant",
            Amount: -12.45m,
            TransactionDate: new DateOnly(2026, 2, 23),
            AllowedSubcategories: subcategories,
            DeterministicResult: new DeterministicClassificationStageResult(
                ProposedSubcategoryId: null,
                Confidence: 0m,
                RationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
                Rationale: "No deterministic rule matched.",
                HasConflict: false,
                Candidates: []),
            SemanticResult: new SemanticRetrievalResult(
                Succeeded: true,
                StatusCode: SemanticRetrievalStatusCodes.NoCandidates,
                StatusMessage: "No semantic candidates met threshold.",
                Candidates: []),
            FusionDecision: new ClassificationConfidenceFusionDecision(
                Decision: ClassificationDecision.NeedsReview,
                ReviewStatus: TransactionReviewStatus.NeedsReview,
                ProposedSubcategoryId: null,
                FinalConfidence: 0m,
                DecisionReasonCode: ClassificationAmbiguityReasonCodes.NoDeterministicMatch,
                DecisionRationale: "Deterministic and semantic stages were insufficient.",
                AgentNoteSummary: null,
                EscalatedToNextStage: true));
    }

    private sealed class StubMafFallbackGraphExecutor(
        Func<CancellationToken, Task<string>> execute) : IMafFallbackGraphExecutor
    {
        public int CallCount { get; private set; }

        public async Task<string> ExecuteAsync(MafFallbackGraphRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return await execute(cancellationToken);
        }
    }
}

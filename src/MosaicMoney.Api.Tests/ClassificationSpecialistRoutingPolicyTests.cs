using Microsoft.Extensions.Options;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class ClassificationSpecialistRegistryTests
{
    [Fact]
    public void Resolve_DefaultOptions_RegistersAllSpecialists()
    {
        var options = new ClassificationSpecialistRegistryOptions();
        var registry = new ClassificationSpecialistRegistry(Options.Create(options));

        Assert.False(registry.RoutingPolicyEnabled);

        foreach (var key in ClassificationSpecialistKeys.All)
        {
            var resolution = registry.Resolve(key);
            Assert.True(resolution.IsRegistered);
            Assert.NotNull(resolution.Registration);
            Assert.Equal(key, resolution.Registration!.SpecialistKey);
        }
    }

    [Fact]
    public void Resolve_MissingSpecialistRegistration_ReturnsUnregistered()
    {
        var options = new ClassificationSpecialistRegistryOptions();
        options.Specialists.Remove(ClassificationSpecialistKeys.Income);

        var registry = new ClassificationSpecialistRegistry(Options.Create(options));
        var resolution = registry.Resolve(ClassificationSpecialistKeys.Income);

        Assert.False(resolution.IsRegistered);
        Assert.Null(resolution.Registration);
    }
}

public sealed class ClassificationSpecialistRoutingPolicyTests
{
    [Fact]
    public void Evaluate_DeterministicCategorized_PreservesDeterministicPrecedence()
    {
        var policy = CreatePolicy(new ClassificationSpecialistRegistryOptions
        {
            EnableRoutingPolicy = true,
        });

        var decision = policy.Evaluate(BuildInput(
            description: "Transfer to checking",
            amount: -42.10m,
            ambiguityDecision: new ClassificationAmbiguityDecision(
                ClassificationDecision.Categorized,
                TransactionReviewStatus.None,
                0.9300m,
                ClassificationAmbiguityReasonCodes.DeterministicAccepted,
                "Deterministic accepted.",
                "Deterministic accepted.")));

        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.DeterministicPrecedencePreserved, decision.DecisionReasonCode);
        Assert.False(decision.OverrideFinalDecisionToNeedsReview);
        Assert.False(decision.AllowSemanticStage);
        Assert.False(decision.AllowMafFallbackStage);
    }

    [Fact]
    public void Evaluate_RoutingPolicyDisabled_UsesCategorizationLaneDefaults()
    {
        var policy = CreatePolicy(new ClassificationSpecialistRegistryOptions
        {
            EnableRoutingPolicy = false,
        });

        var decision = policy.Evaluate(BuildInput(
            description: "Payroll direct deposit",
            amount: 2200.00m,
            ambiguityDecision: BuildNeedsReviewAmbiguityDecision()));

        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.RoutingPolicyDisabled, decision.DecisionReasonCode);
        Assert.Equal(ClassificationSpecialistKeys.Categorization, decision.EffectiveSpecialistKey);
        Assert.False(decision.OverrideFinalDecisionToNeedsReview);
        Assert.True(decision.AllowSemanticStage);
        Assert.True(decision.AllowMafFallbackStage);
    }

    [Fact]
    public void Evaluate_IncomeLane_RoutesToSpecialistNeedsReview()
    {
        var policy = CreatePolicy(new ClassificationSpecialistRegistryOptions
        {
            EnableRoutingPolicy = true,
        });

        var decision = policy.Evaluate(BuildInput(
            description: "Payroll direct deposit",
            amount: 2200.00m,
            ambiguityDecision: BuildNeedsReviewAmbiguityDecision()));

        Assert.Equal(ClassificationSpecialistKeys.Income, decision.RequestedSpecialistKey);
        Assert.Equal(ClassificationSpecialistKeys.Income, decision.EffectiveSpecialistKey);
        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.SpecialistEscalationRequired, decision.DecisionReasonCode);
        Assert.True(decision.OverrideFinalDecisionToNeedsReview);
        Assert.False(decision.AllowSemanticStage);
        Assert.False(decision.AllowMafFallbackStage);
    }

    [Fact]
    public void Evaluate_DisabledSpecialist_RoutesFailClosed()
    {
        var options = new ClassificationSpecialistRegistryOptions
        {
            EnableRoutingPolicy = true,
        };

        options.Specialists[ClassificationSpecialistKeys.Transfer] = new ClassificationSpecialistRegistrationOptions
        {
            SpecialistId = ClassificationSpecialistKeys.Transfer,
            Enabled = false,
            AllowSemanticStage = false,
            AllowMafFallbackStage = false,
        };

        var policy = CreatePolicy(options);

        var decision = policy.Evaluate(BuildInput(
            description: "Transfer to savings",
            amount: -14.25m,
            ambiguityDecision: BuildNeedsReviewAmbiguityDecision()));

        Assert.Equal(ClassificationSpecialistKeys.Transfer, decision.RequestedSpecialistKey);
        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.SpecialistDisabled, decision.DecisionReasonCode);
        Assert.True(decision.OverrideFinalDecisionToNeedsReview);
        Assert.False(decision.AllowSemanticStage);
        Assert.False(decision.AllowMafFallbackStage);
    }

    [Fact]
    public void Evaluate_UnregisteredSpecialist_RoutesFailClosed()
    {
        var options = new ClassificationSpecialistRegistryOptions
        {
            EnableRoutingPolicy = true,
        };

        options.Specialists.Remove(ClassificationSpecialistKeys.Income);
        var policy = CreatePolicy(options);

        var decision = policy.Evaluate(BuildInput(
            description: "Payroll direct deposit",
            amount: 2200.00m,
            ambiguityDecision: BuildNeedsReviewAmbiguityDecision()));

        Assert.Equal(ClassificationSpecialistKeys.Income, decision.RequestedSpecialistKey);
        Assert.Equal(ClassificationSpecialistRoutingReasonCodes.SpecialistNotRegistered, decision.DecisionReasonCode);
        Assert.True(decision.OverrideFinalDecisionToNeedsReview);
        Assert.False(decision.AllowSemanticStage);
        Assert.False(decision.AllowMafFallbackStage);
    }

    private static IClassificationSpecialistRoutingPolicy CreatePolicy(ClassificationSpecialistRegistryOptions options)
    {
        return new ClassificationSpecialistRoutingPolicy(new ClassificationSpecialistRegistry(Options.Create(options)));
    }

    private static ClassificationSpecialistRoutingInput BuildInput(
        string description,
        decimal amount,
        ClassificationAmbiguityDecision ambiguityDecision)
    {
        return new ClassificationSpecialistRoutingInput(
            Description: description,
            Amount: amount,
            CurrentReviewStatus: TransactionReviewStatus.None,
            DeterministicResult: new DeterministicClassificationStageResult(
                ProposedSubcategoryId: null,
                Confidence: 0m,
                RationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
                Rationale: "Deterministic stage did not produce a confident match.",
                HasConflict: false,
                Candidates: []),
            AmbiguityDecision: ambiguityDecision);
    }

    private static ClassificationAmbiguityDecision BuildNeedsReviewAmbiguityDecision()
    {
        return new ClassificationAmbiguityDecision(
            ClassificationDecision.NeedsReview,
            TransactionReviewStatus.NeedsReview,
            0m,
            ClassificationAmbiguityReasonCodes.NoDeterministicMatch,
            "No deterministic match.",
            "No deterministic match.");
    }
}

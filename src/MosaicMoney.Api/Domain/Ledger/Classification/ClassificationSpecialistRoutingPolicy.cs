using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class ClassificationSpecialistKeys
{
    public const string Categorization = "categorization";
    public const string Transfer = "transfer";
    public const string Income = "income";
    public const string DebtQuality = "debt-quality";
    public const string Investment = "investment";
    public const string Anomaly = "anomaly";

    public static readonly IReadOnlyList<string> All =
    [
        Categorization,
        Transfer,
        Income,
        DebtQuality,
        Investment,
        Anomaly,
    ];
}

public sealed class ClassificationSpecialistRegistrationOptions
{
    public string SpecialistId { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public bool AllowSemanticStage { get; set; } = true;

    public bool AllowMafFallbackStage { get; set; } = true;
}

public sealed class ClassificationSpecialistRegistryOptions
{
    public const string SectionName = "AiWorkflow:ClassificationSpecialists";

    public bool EnableRoutingPolicy { get; set; }

    public Dictionary<string, ClassificationSpecialistRegistrationOptions> Specialists { get; set; } =
        BuildDefaultRegistrations();

    private static Dictionary<string, ClassificationSpecialistRegistrationOptions> BuildDefaultRegistrations()
    {
        return new Dictionary<string, ClassificationSpecialistRegistrationOptions>(StringComparer.OrdinalIgnoreCase)
        {
            [ClassificationSpecialistKeys.Categorization] = new ClassificationSpecialistRegistrationOptions
            {
                SpecialistId = ClassificationSpecialistKeys.Categorization,
                Enabled = true,
                AllowSemanticStage = true,
                AllowMafFallbackStage = true,
            },
            [ClassificationSpecialistKeys.Transfer] = new ClassificationSpecialistRegistrationOptions
            {
                SpecialistId = ClassificationSpecialistKeys.Transfer,
                Enabled = true,
                AllowSemanticStage = false,
                AllowMafFallbackStage = false,
            },
            [ClassificationSpecialistKeys.Income] = new ClassificationSpecialistRegistrationOptions
            {
                SpecialistId = ClassificationSpecialistKeys.Income,
                Enabled = true,
                AllowSemanticStage = false,
                AllowMafFallbackStage = false,
            },
            [ClassificationSpecialistKeys.DebtQuality] = new ClassificationSpecialistRegistrationOptions
            {
                SpecialistId = ClassificationSpecialistKeys.DebtQuality,
                Enabled = true,
                AllowSemanticStage = false,
                AllowMafFallbackStage = false,
            },
            [ClassificationSpecialistKeys.Investment] = new ClassificationSpecialistRegistrationOptions
            {
                SpecialistId = ClassificationSpecialistKeys.Investment,
                Enabled = true,
                AllowSemanticStage = false,
                AllowMafFallbackStage = false,
            },
            [ClassificationSpecialistKeys.Anomaly] = new ClassificationSpecialistRegistrationOptions
            {
                SpecialistId = ClassificationSpecialistKeys.Anomaly,
                Enabled = true,
                AllowSemanticStage = false,
                AllowMafFallbackStage = false,
            },
        };
    }
}

public sealed record ClassificationSpecialistRegistration(
    string SpecialistKey,
    string SpecialistId,
    bool Enabled,
    bool AllowSemanticStage,
    bool AllowMafFallbackStage);

public sealed record ClassificationSpecialistResolution(
    bool IsRegistered,
    ClassificationSpecialistRegistration? Registration);

public interface IClassificationSpecialistRegistry
{
    bool RoutingPolicyEnabled { get; }

    ClassificationSpecialistResolution Resolve(string specialistKey);
}

public sealed class ClassificationSpecialistRegistry(
    IOptions<ClassificationSpecialistRegistryOptions> options) : IClassificationSpecialistRegistry
{
    public bool RoutingPolicyEnabled => options.Value.EnableRoutingPolicy;

    public ClassificationSpecialistResolution Resolve(string specialistKey)
    {
        if (string.IsNullOrWhiteSpace(specialistKey))
        {
            return new ClassificationSpecialistResolution(false, null);
        }

        var configured = options.Value.Specialists;
        if (configured is null || !configured.TryGetValue(specialistKey, out var registrationOptions))
        {
            return new ClassificationSpecialistResolution(false, null);
        }

        var normalizedSpecialistKey = specialistKey.Trim().ToLowerInvariant();
        var specialistId = string.IsNullOrWhiteSpace(registrationOptions.SpecialistId)
            ? normalizedSpecialistKey
            : registrationOptions.SpecialistId.Trim();

        var registration = new ClassificationSpecialistRegistration(
            normalizedSpecialistKey,
            specialistId,
            registrationOptions.Enabled,
            registrationOptions.AllowSemanticStage,
            registrationOptions.AllowMafFallbackStage);

        return new ClassificationSpecialistResolution(true, registration);
    }
}

public static class ClassificationSpecialistRoutingReasonCodes
{
    public const string RoutingPolicyDisabled = "routing_policy_disabled";
    public const string DeterministicPrecedencePreserved = "routing_deterministic_precedence";
    public const string SpecialistNotRegistered = "routing_specialist_not_registered";
    public const string SpecialistDisabled = "routing_specialist_disabled";
    public const string SpecialistEscalationRequired = "routing_specialist_escalation_required";
}

public sealed record ClassificationSpecialistRoutingInput(
    string Description,
    decimal Amount,
    TransactionReviewStatus CurrentReviewStatus,
    DeterministicClassificationStageResult DeterministicResult,
    ClassificationAmbiguityDecision AmbiguityDecision);

public sealed record ClassificationSpecialistRoutingDecision(
    string RequestedSpecialistKey,
    string EffectiveSpecialistKey,
    string? SpecialistId,
    bool AllowSemanticStage,
    bool AllowMafFallbackStage,
    bool OverrideFinalDecisionToNeedsReview,
    string DecisionReasonCode,
    string DecisionRationale,
    string? AgentNoteSummary);

public interface IClassificationSpecialistRoutingPolicy
{
    ClassificationSpecialistRoutingDecision Evaluate(ClassificationSpecialistRoutingInput input);
}

public sealed class ClassificationSpecialistRoutingPolicy(
    IClassificationSpecialistRegistry specialistRegistry) : IClassificationSpecialistRoutingPolicy
{
    private static readonly string[] TransferRoutingTerms =
    [
        "transfer",
        "zelle",
        "venmo",
        "cashapp",
        "cash app",
        "ach",
    ];

    private static readonly string[] IncomeRoutingTerms =
    [
        "payroll",
        "salary",
        "paycheck",
        "direct deposit",
        "income",
        "refund",
    ];

    private static readonly string[] DebtQualityRoutingTerms =
    [
        "interest",
        "apr",
        "minimum payment",
        "late fee",
        "finance charge",
        "loan payment",
    ];

    private static readonly string[] InvestmentRoutingTerms =
    [
        "brokerage",
        "dividend",
        "stock",
        "etf",
        "mutual fund",
        "robinhood",
        "fidelity",
        "vanguard",
        "schwab",
    ];

    private static readonly string[] AnomalyRoutingTerms =
    [
        "chargeback",
        "reversal",
        "dispute",
        "fraud",
        "duplicate",
        "unknown merchant",
    ];

    public ClassificationSpecialistRoutingDecision Evaluate(ClassificationSpecialistRoutingInput input)
    {
        if (input.AmbiguityDecision.Decision == ClassificationDecision.Categorized)
        {
            return new ClassificationSpecialistRoutingDecision(
                ClassificationSpecialistKeys.Categorization,
                ClassificationSpecialistKeys.Categorization,
                ClassificationSpecialistKeys.Categorization,
                AllowSemanticStage: false,
                AllowMafFallbackStage: false,
                OverrideFinalDecisionToNeedsReview: false,
                ClassificationSpecialistRoutingReasonCodes.DeterministicPrecedencePreserved,
                "Deterministic precedence resolved the transaction before specialist routing escalation was required.",
                "Deterministic precedence preserved categorized outcome.");
        }

        if (!specialistRegistry.RoutingPolicyEnabled)
        {
            return new ClassificationSpecialistRoutingDecision(
                ClassificationSpecialistKeys.Categorization,
                ClassificationSpecialistKeys.Categorization,
                ClassificationSpecialistKeys.Categorization,
                AllowSemanticStage: true,
                AllowMafFallbackStage: true,
                OverrideFinalDecisionToNeedsReview: false,
                ClassificationSpecialistRoutingReasonCodes.RoutingPolicyDisabled,
                "Specialist routing policy is disabled; classification uses categorization lane defaults.",
                null);
        }

        var requestedSpecialistKey = SelectRequestedSpecialist(input);
        var resolution = specialistRegistry.Resolve(requestedSpecialistKey);
        if (!resolution.IsRegistered || resolution.Registration is null)
        {
            return BuildFailClosedDecision(
                requestedSpecialistKey,
                ClassificationSpecialistRoutingReasonCodes.SpecialistNotRegistered,
                $"Specialist registry entry '{requestedSpecialistKey}' is missing; route to NeedsReview fail-closed.");
        }

        var registration = resolution.Registration;
        if (!registration.Enabled)
        {
            return BuildFailClosedDecision(
                requestedSpecialistKey,
                ClassificationSpecialistRoutingReasonCodes.SpecialistDisabled,
                $"Specialist '{registration.SpecialistKey}' is disabled in registry; route to NeedsReview fail-closed.");
        }

        if (string.Equals(registration.SpecialistKey, ClassificationSpecialistKeys.Categorization, StringComparison.OrdinalIgnoreCase))
        {
            return new ClassificationSpecialistRoutingDecision(
                requestedSpecialistKey,
                registration.SpecialistKey,
                registration.SpecialistId,
                registration.AllowSemanticStage,
                registration.AllowMafFallbackStage,
                OverrideFinalDecisionToNeedsReview: false,
                ClassificationSpecialistRoutingReasonCodes.DeterministicPrecedencePreserved,
                "Specialist routing selected categorization lane; semantic and MAF stages remain available.",
                null);
        }

        return new ClassificationSpecialistRoutingDecision(
            requestedSpecialistKey,
            registration.SpecialistKey,
            registration.SpecialistId,
            AllowSemanticStage: false,
            AllowMafFallbackStage: false,
            OverrideFinalDecisionToNeedsReview: true,
            ClassificationSpecialistRoutingReasonCodes.SpecialistEscalationRequired,
            $"Specialist routing selected '{registration.SpecialistKey}' lane ({registration.SpecialistId}); categorization pipeline remains fail-closed in NeedsReview pending specialist execution.",
            $"Specialist routing escalated transaction to {registration.SpecialistKey} lane.");
    }

    private static ClassificationSpecialistRoutingDecision BuildFailClosedDecision(
        string requestedSpecialistKey,
        string reasonCode,
        string rationale)
    {
        return new ClassificationSpecialistRoutingDecision(
            requestedSpecialistKey,
            ClassificationSpecialistKeys.Categorization,
            null,
            AllowSemanticStage: false,
            AllowMafFallbackStage: false,
            OverrideFinalDecisionToNeedsReview: true,
            reasonCode,
            rationale,
            $"Specialist routing failed closed for lane {requestedSpecialistKey}.");
    }

    private static string SelectRequestedSpecialist(ClassificationSpecialistRoutingInput input)
    {
        if (input.DeterministicResult.HasConflict)
        {
            return ClassificationSpecialistKeys.Anomaly;
        }

        var normalizedDescription = input.Description.Trim().ToLowerInvariant();
        if (ContainsAnyTerm(normalizedDescription, AnomalyRoutingTerms))
        {
            return ClassificationSpecialistKeys.Anomaly;
        }

        if (ContainsAnyTerm(normalizedDescription, TransferRoutingTerms))
        {
            return ClassificationSpecialistKeys.Transfer;
        }

        if (ContainsAnyTerm(normalizedDescription, InvestmentRoutingTerms))
        {
            return ClassificationSpecialistKeys.Investment;
        }

        if (ContainsAnyTerm(normalizedDescription, DebtQualityRoutingTerms))
        {
            return ClassificationSpecialistKeys.DebtQuality;
        }

        if (input.Amount > 0m || ContainsAnyTerm(normalizedDescription, IncomeRoutingTerms))
        {
            return ClassificationSpecialistKeys.Income;
        }

        return ClassificationSpecialistKeys.Categorization;
    }

    private static bool ContainsAnyTerm(string normalizedDescription, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (normalizedDescription.Contains(term, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
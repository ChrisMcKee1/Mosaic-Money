namespace MosaicMoney.Api.Domain.Ledger.Classification;

public sealed class FoundryClassificationOptions
{
    public const string SectionName = "AiWorkflow:Classification:Foundry";

    public bool Enabled { get; init; }

    public string Endpoint { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string Deployment { get; init; } = "gpt-5.3-codex";

    public string AgentName { get; init; } = "mosaic-money-classifier";

    public string ReasoningEffort { get; init; } = "low";

    public decimal MinimumConfidenceForAutoAssign { get; init; } = 0.80m;

    public bool IsConfigured()
    {
        return Enabled
            && !string.IsNullOrWhiteSpace(Endpoint)
            && !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(Deployment);
    }

    public string GetResponsesEndpoint()
    {
        var trimmed = Endpoint.Trim().TrimEnd('/');

        if (trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"{trimmed}/openai/v1";
    }
}

public sealed record FoundryClassificationInput(
    Guid TransactionId,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    IReadOnlyList<DeterministicClassificationSubcategory> AllowedSubcategories,
    string HouseholdScopeSummary,
    FoundryPlaidContext? PlaidContext = null);

public sealed record FoundryPlaidContext(
    string? MerchantName,
    string? PaymentChannel,
    string? CategoryPrimary,
    string? CategoryDetailed,
    string? CounterpartyName,
    string? CounterpartyType);

public sealed record FoundryClassificationDecision(
    ClassificationDecision Decision,
    TransactionReviewStatus ReviewStatus,
    Guid? ProposedSubcategoryId,
    decimal Confidence,
    string ReasonCode,
    string Rationale,
    string? AgentNoteSummary,
    string AssignmentSource,
    string? AssignmentAgent,
    string RawOutputText);

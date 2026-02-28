#pragma warning disable OPENAI001
using System.ClientModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public interface IFoundryClassificationService
{
    Task<FoundryClassificationDecision?> ClassifyAsync(
        FoundryClassificationInput input,
        CancellationToken cancellationToken = default);
}

public sealed class FoundryClassificationService(
    IOptions<FoundryClassificationOptions> options,
    ILogger<FoundryClassificationService> logger) : IFoundryClassificationService
{
    public async Task<FoundryClassificationDecision?> ClassifyAsync(
        FoundryClassificationInput input,
        CancellationToken cancellationToken = default)
    {
        var foundryOptions = options.Value;
        if (!foundryOptions.IsConfigured())
        {
            return null;
        }

        var prompt = BuildPrompt(input);

        ResponseResult response;
        try
        {
            var client = new ResponsesClient(
                credential: new ApiKeyCredential(foundryOptions.ApiKey),
                options: new OpenAIClientOptions
                {
                    Endpoint = new Uri(foundryOptions.GetResponsesEndpoint()),
                });

            var responseResult = await client.CreateResponseAsync(
                model: foundryOptions.Deployment,
                userInputText: prompt,
                previousResponseId: null,
                cancellationToken: cancellationToken);

            response = responseResult.Value;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Foundry classification request failed for transaction {TransactionId}.", input.TransactionId);
            return null;
        }

        var output = response.GetOutputText();
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        if (!TryParseDecision(output, input, foundryOptions, out var decision))
        {
            logger.LogWarning(
                "Foundry classification returned non-parseable JSON for transaction {TransactionId}. Output: {Output}",
                input.TransactionId,
                output);
            return null;
        }

        return decision;
    }

    internal static string BuildPrompt(FoundryClassificationInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are a financial transaction classification agent for Mosaic Money.");
        builder.AppendLine("Follow single-entry ledger semantics and fail closed for ambiguity.");
        builder.AppendLine("If uncertain, return needs_review with null proposedSubcategoryId.");
        builder.AppendLine();
        builder.AppendLine("Transaction:");
        builder.AppendLine($"- transactionId: {input.TransactionId:D}");
        builder.AppendLine($"- description: {input.Description}");
        builder.AppendLine($"- amount: {input.Amount:F2}");
        builder.AppendLine($"- transactionDate: {input.TransactionDate:yyyy-MM-dd}");
        builder.AppendLine($"- scope: {input.HouseholdScopeSummary}");

        AppendPlaidContextBlock(builder, input.PlaidContext);

        builder.AppendLine();
        builder.AppendLine("Allowed subcategories (pick proposedSubcategoryId from this list or null):");

        foreach (var subcategory in input.AllowedSubcategories)
        {
            builder.AppendLine($"- {subcategory.Id:D}: {subcategory.Name}");
        }

        builder.AppendLine();
        builder.AppendLine("Return strict JSON only with this shape:");
        builder.AppendLine("{");
        builder.AppendLine("  \"decision\": \"categorized\" | \"needs_review\",");
        builder.AppendLine("  \"proposedSubcategoryId\": \"<guid>\" | null,");
        builder.AppendLine("  \"confidence\": <decimal between 0 and 1>,");
        builder.AppendLine("  \"reasonCode\": \"short_snake_case_code\",");
        builder.AppendLine("  \"rationale\": \"brief rationale\",");
        builder.AppendLine("  \"agentNoteSummary\": \"concise summary without transcript content\" | null");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendPlaidContextBlock(StringBuilder builder, FoundryPlaidContext? plaidContext)
    {
        if (plaidContext is null)
        {
            return;
        }

        var contextLines = new List<string>();

        if (!string.IsNullOrWhiteSpace(plaidContext.MerchantName))
        {
            contextLines.Add($"- merchantName: {plaidContext.MerchantName}");
        }

        if (!string.IsNullOrWhiteSpace(plaidContext.PaymentChannel))
        {
            contextLines.Add($"- paymentChannel: {plaidContext.PaymentChannel}");
        }

        if (!string.IsNullOrWhiteSpace(plaidContext.CategoryPrimary))
        {
            contextLines.Add($"- categoryPrimary: {plaidContext.CategoryPrimary}");
        }

        if (!string.IsNullOrWhiteSpace(plaidContext.CategoryDetailed))
        {
            contextLines.Add($"- categoryDetailed: {plaidContext.CategoryDetailed}");
        }

        if (!string.IsNullOrWhiteSpace(plaidContext.CounterpartyName))
        {
            contextLines.Add($"- counterpartyName: {plaidContext.CounterpartyName}");
        }

        if (!string.IsNullOrWhiteSpace(plaidContext.CounterpartyType))
        {
            contextLines.Add($"- counterpartyType: {plaidContext.CounterpartyType}");
        }

        if (contextLines.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Optional Plaid context (safe, non-secret hints; may be absent):");

        foreach (var line in contextLines)
        {
            builder.AppendLine(line);
        }
    }

    private static bool TryParseDecision(
        string output,
        FoundryClassificationInput input,
        FoundryClassificationOptions options,
        out FoundryClassificationDecision? decision)
    {
        decision = null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(output);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            var decisionText = root.TryGetProperty("decision", out var decisionElement)
                ? decisionElement.GetString()?.Trim().ToLowerInvariant()
                : null;

            if (decisionText is not ("categorized" or "needs_review"))
            {
                return false;
            }

            var proposedId = TryGetGuid(root, "proposedSubcategoryId");
            var confidence = root.TryGetProperty("confidence", out var confidenceElement)
                && confidenceElement.ValueKind == JsonValueKind.Number
                && confidenceElement.TryGetDecimal(out var parsedConfidence)
                    ? parsedConfidence
                    : 0m;

            confidence = decimal.Clamp(confidence, 0m, 1m);

            var isAllowedProposedId = !proposedId.HasValue
                || input.AllowedSubcategories.Any(x => x.Id == proposedId.Value);
            if (!isAllowedProposedId)
            {
                proposedId = null;
                decisionText = "needs_review";
                confidence = 0m;
            }

            var reasonCode = root.TryGetProperty("reasonCode", out var reasonCodeElement)
                ? reasonCodeElement.GetString()
                : null;
            var rationale = root.TryGetProperty("rationale", out var rationaleElement)
                ? rationaleElement.GetString()
                : null;
            var agentNoteSummary = root.TryGetProperty("agentNoteSummary", out var noteElement)
                ? noteElement.GetString()
                : null;

            var categorized = decisionText == "categorized"
                && proposedId.HasValue
                && confidence >= options.MinimumConfidenceForAutoAssign;

            var finalDecision = categorized
                ? ClassificationDecision.Categorized
                : ClassificationDecision.NeedsReview;

            var reviewStatus = categorized
                ? TransactionReviewStatus.Reviewed
                : TransactionReviewStatus.NeedsReview;

            decision = new FoundryClassificationDecision(
                finalDecision,
                reviewStatus,
                categorized ? proposedId : null,
                confidence,
                string.IsNullOrWhiteSpace(reasonCode) ? "foundry_response" : Truncate(reasonCode, 120),
                string.IsNullOrWhiteSpace(rationale)
                    ? "Foundry response processed for transaction classification."
                    : Truncate(rationale, 500),
                AgentNoteSummaryPolicy.Sanitize(agentNoteSummary),
                AssignmentSource: "foundry_responses_api",
                AssignmentAgent: options.AgentName,
                RawOutputText: Truncate(output, 4000));

            return true;
        }
    }

    private static Guid? TryGetGuid(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Guid.TryParse(value.GetString(), out var parsedGuid)
            ? parsedGuid
            : null;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

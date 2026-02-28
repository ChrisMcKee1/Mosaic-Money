using MosaicMoney.Api.Domain.Ledger;
using System.Globalization;

namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

internal static class TransactionSemanticSearchDocument
{
    internal static string BuildSearchDocument(EnrichedTransaction transaction)
    {
        return BuildSearchDocument(transaction.Description, transaction.Amount, transaction.UserNote, transaction.AgentNote);
    }

    internal static string BuildSearchDocument(string description, decimal amount, string? userNote, string? agentNote)
    {
        var parts = new List<string>(5);

        AddPart(parts, "Description", description);
        AddAmountParts(parts, amount);
        AddPart(parts, "UserNote", userNote);
        AddPart(parts, "AgentNote", agentNote);

        return string.Join(" | ", parts);
    }

    internal static string ComputeHash(EnrichedTransaction transaction)
    {
        return ComputeHash(transaction.Description, transaction.Amount, transaction.UserNote, transaction.AgentNote);
    }

    internal static string ComputeHash(string description, decimal amount, string? userNote, string? agentNote)
    {
        return EmbeddingTextHasher.ComputeHash(BuildSearchDocument(description, amount, userNote, agentNote));
    }

    private static void AddPart(List<string> parts, string label, string? value)
    {
        var normalized = EmbeddingTextHasher.Normalize(value);
        if (normalized.Length == 0)
        {
            return;
        }

        parts.Add($"{label}: {normalized}");
    }

    private static void AddAmountParts(List<string> parts, decimal amount)
    {
        // Include signed and absolute normalized values to support numeric search intents.
        var signed = decimal.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
        var absolute = decimal.Round(decimal.Abs(amount), 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

        parts.Add($"Amount: {signed}");
        parts.Add($"AmountAbsolute: {absolute}");
    }
}

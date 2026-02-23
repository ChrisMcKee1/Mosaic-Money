using System.Text;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class DeterministicClassificationReasonCodes
{
    public const string MissingDescription = "deterministic_missing_description";
    public const string NonExpenseAmount = "deterministic_non_expense_amount";
    public const string NoRuleMatch = "deterministic_no_rule_match";
    public const string KeywordMatch = "deterministic_keyword_match";
    public const string ConflictingRules = "deterministic_conflicting_rules";
}

public sealed record DeterministicClassificationSubcategory(Guid Id, string Name);

public sealed record DeterministicClassificationRequest(
    Guid TransactionId,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    IReadOnlyList<DeterministicClassificationSubcategory> Subcategories);

public sealed record DeterministicClassificationCandidate(
    Guid SubcategoryId,
    string SubcategoryName,
    decimal Confidence,
    IReadOnlyList<string> MatchedTokens);

public sealed record DeterministicClassificationStageResult(
    Guid? ProposedSubcategoryId,
    decimal Confidence,
    string RationaleCode,
    string Rationale,
    bool HasConflict,
    IReadOnlyList<DeterministicClassificationCandidate> Candidates);

public interface IDeterministicClassificationEngine
{
    DeterministicClassificationStageResult Execute(DeterministicClassificationRequest request);
}

public sealed class DeterministicClassificationEngine : IDeterministicClassificationEngine
{
    private const decimal ScoreConflictDelta = 0.0500m;

    public DeterministicClassificationStageResult Execute(DeterministicClassificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return new DeterministicClassificationStageResult(
                ProposedSubcategoryId: null,
                Confidence: 0m,
                RationaleCode: DeterministicClassificationReasonCodes.MissingDescription,
                Rationale: "Transaction description is empty so deterministic classification cannot evaluate rules.",
                HasConflict: false,
                Candidates: []);
        }

        if (request.Amount >= 0)
        {
            return new DeterministicClassificationStageResult(
                ProposedSubcategoryId: null,
                Confidence: 0.2000m,
                RationaleCode: DeterministicClassificationReasonCodes.NonExpenseAmount,
                Rationale: "Deterministic stage is currently limited to expense-like transactions with negative amounts.",
                HasConflict: false,
                Candidates: []);
        }

        var descriptionTokens = Tokenize(request.Description);
        if (descriptionTokens.Count == 0)
        {
            return new DeterministicClassificationStageResult(
                ProposedSubcategoryId: null,
                Confidence: 0m,
                RationaleCode: DeterministicClassificationReasonCodes.MissingDescription,
                Rationale: "Transaction description did not contain usable tokens for deterministic matching.",
                HasConflict: false,
                Candidates: []);
        }

        var normalizedDescriptionPhrase = NormalizePhrase(request.Description);
        var candidates = request.Subcategories
            .Select(subcategory => BuildCandidate(subcategory, descriptionTokens, normalizedDescriptionPhrase))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.SubcategoryName, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
        {
            return new DeterministicClassificationStageResult(
                ProposedSubcategoryId: null,
                Confidence: 0m,
                RationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
                Rationale: "No deterministic subcategory rule matched the transaction description.",
                HasConflict: false,
                Candidates: []);
        }

        var topCandidate = candidates[0];
        if (candidates.Count >= 2 && topCandidate.Confidence - candidates[1].Confidence <= ScoreConflictDelta)
        {
            var conflictSummary = string.Join(
                ", ",
                candidates.Take(3).Select(candidate => $"{candidate.SubcategoryName} ({candidate.Confidence:F4})"));

            return new DeterministicClassificationStageResult(
                ProposedSubcategoryId: null,
                Confidence: topCandidate.Confidence,
                RationaleCode: DeterministicClassificationReasonCodes.ConflictingRules,
                Rationale: $"Deterministic stage found competing matches with similar confidence: {conflictSummary}.",
                HasConflict: true,
                Candidates: candidates.Take(5).ToList());
        }

        return new DeterministicClassificationStageResult(
            ProposedSubcategoryId: topCandidate.SubcategoryId,
            Confidence: topCandidate.Confidence,
            RationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
            Rationale: $"Deterministic stage matched subcategory '{topCandidate.SubcategoryName}' using tokens: {string.Join(", ", topCandidate.MatchedTokens)}.",
            HasConflict: false,
            Candidates: candidates.Take(5).ToList());
    }

    private static DeterministicClassificationCandidate? BuildCandidate(
        DeterministicClassificationSubcategory subcategory,
        IReadOnlySet<string> descriptionTokens,
        string normalizedDescriptionPhrase)
    {
        var subcategoryTokens = Tokenize(subcategory.Name);
        if (subcategoryTokens.Count == 0)
        {
            return null;
        }

        var matchedTokens = subcategoryTokens
            .Where(descriptionTokens.Contains)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToList();

        if (matchedTokens.Count == 0)
        {
            return null;
        }

        var tokenCoverage = matchedTokens.Count / (decimal)subcategoryTokens.Count;
        var normalizedSubcategoryPhrase = NormalizePhrase(subcategory.Name);
        var phraseBoost = normalizedSubcategoryPhrase.Length > 0 && normalizedDescriptionPhrase.Contains(normalizedSubcategoryPhrase, StringComparison.Ordinal)
            ? 0.2000m
            : 0m;

        var confidence = decimal.Min(1m, decimal.Round(0.3500m + (tokenCoverage * 0.5500m) + phraseBoost, 4, MidpointRounding.AwayFromZero));

        return new DeterministicClassificationCandidate(
            subcategory.Id,
            subcategory.Name,
            confidence,
            matchedTokens);
    }

    private static IReadOnlySet<string> Tokenize(string value)
    {
        return value
            .Split([' ', '\t', '\r', '\n', '-', '/', '\\', '_', '.', ',', ':', ';', '(', ')', '[', ']', '{', '}', '!', '?', '&', '+', '*', '"', '\''], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Where(token => token.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string NormalizeToken(string token)
    {
        var builder = new StringBuilder(token.Length);

        foreach (var character in token)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string NormalizePhrase(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}

using System.Text.RegularExpressions;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class AgentNoteSummaryPolicy
{
    public const int MaxPersistedSummaryLength = 280;
    public const string SuppressedSummary = "Agent summary suppressed by policy; see stage rationale for details.";

    private static readonly Regex RoleMarkerRegex = new(
        @"\b(user|assistant|system|tool)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? Sanitize(string? rawSummary)
    {
        if (string.IsNullOrWhiteSpace(rawSummary))
        {
            return null;
        }

        var trimmed = rawSummary.Trim();
        if (LooksLikeTranscriptOrToolDump(trimmed))
        {
            return SuppressedSummary;
        }

        var normalized = WhitespaceRegex.Replace(trimmed, " ").Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= MaxPersistedSummaryLength
            ? normalized
            : normalized[..MaxPersistedSummaryLength];
    }

    private static bool LooksLikeTranscriptOrToolDump(string value)
    {
        if (value.Contains("```", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Split('\n', StringSplitOptions.None).Length >= 6)
        {
            return true;
        }

        if (RoleMarkerRegex.Matches(value).Count >= 2)
        {
            return true;
        }

        if (value.Length >= 160
            && (value.Contains("tool output", StringComparison.OrdinalIgnoreCase)
                || value.Contains("tool call", StringComparison.OrdinalIgnoreCase)
                || value.Contains("function call", StringComparison.OrdinalIgnoreCase)
                || value.Contains("stdout", StringComparison.OrdinalIgnoreCase)
                || value.Contains("stderr", StringComparison.OrdinalIgnoreCase)
                || value.Contains("stack trace", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (value.Length >= 180
            && value.Contains('{', StringComparison.Ordinal)
            && value.Contains('}', StringComparison.Ordinal)
            && (value.Contains("\"tool\"", StringComparison.OrdinalIgnoreCase)
                || value.Contains("\"arguments\"", StringComparison.OrdinalIgnoreCase)
                || value.Contains("\"result\"", StringComparison.OrdinalIgnoreCase)
                || value.Contains("\"output\"", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}

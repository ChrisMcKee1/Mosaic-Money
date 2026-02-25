using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgentNoteSummaryPolicyTests
{
    [Fact]
    public void Sanitize_ConciseSummary_NormalizesWhitespaceAndPreservesContent()
    {
        var raw = "  Candidate routed to NeedsReview   due to confidence gap.  ";

        var sanitized = AgentNoteSummaryPolicy.Sanitize(raw);

        Assert.Equal("Candidate routed to NeedsReview due to confidence gap.", sanitized);
    }

    [Fact]
    public void Sanitize_TranscriptMarkers_ReturnsSuppressedSummary()
    {
        var raw = "User: classify transaction. Assistant: checking evidence. Tool output: {\"tool\":\"semantic\",\"result\":\"...\"}";

        var sanitized = AgentNoteSummaryPolicy.Sanitize(raw);

        Assert.Equal(AgentNoteSummaryPolicy.SuppressedSummary, sanitized);
    }

    [Fact]
    public void Sanitize_VeryLongSummary_IsBoundedToConfiguredLength()
    {
        var raw = new string('a', AgentNoteSummaryPolicy.MaxPersistedSummaryLength + 50);

        var sanitized = AgentNoteSummaryPolicy.Sanitize(raw);

        Assert.NotNull(sanitized);
        Assert.Equal(AgentNoteSummaryPolicy.MaxPersistedSummaryLength, sanitized!.Length);
    }
}

using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class DeterministicClassificationEngineTests
{
    private readonly DeterministicClassificationEngine _engine = new();

    [Fact]
    public void Execute_ClearlyMatchedExpense_ReturnsCategorizationCandidate()
    {
        var request = new DeterministicClassificationRequest(
            Guid.NewGuid(),
            "Austin Energy bill payment",
            -145.32m,
            new DateOnly(2026, 2, 23),
            [
                new DeterministicClassificationSubcategory(Guid.NewGuid(), "Austin Energy"),
                new DeterministicClassificationSubcategory(Guid.NewGuid(), "Groceries"),
            ]);

        var result = _engine.Execute(request);

        Assert.False(result.HasConflict);
        Assert.Equal(DeterministicClassificationReasonCodes.KeywordMatch, result.RationaleCode);
        Assert.True(result.ProposedSubcategoryId.HasValue);
        Assert.True(result.Confidence >= 0.8500m);
        Assert.NotEmpty(result.Candidates);
    }

    [Fact]
    public void Execute_ConflictingTopCandidates_FailsClosedToConflictResult()
    {
        var request = new DeterministicClassificationRequest(
            Guid.NewGuid(),
            "HEB purchase",
            -42.00m,
            new DateOnly(2026, 2, 23),
            [
                new DeterministicClassificationSubcategory(Guid.NewGuid(), "HEB Grocery"),
                new DeterministicClassificationSubcategory(Guid.NewGuid(), "HEB Fuel"),
            ]);

        var result = _engine.Execute(request);

        Assert.True(result.HasConflict);
        Assert.Equal(DeterministicClassificationReasonCodes.ConflictingRules, result.RationaleCode);
        Assert.Null(result.ProposedSubcategoryId);
        Assert.NotEmpty(result.Candidates);
    }

    [Fact]
    public void Execute_NonExpenseAmount_ReturnsNoDeterministicClassification()
    {
        var request = new DeterministicClassificationRequest(
            Guid.NewGuid(),
            "Paycheck direct deposit",
            1600.00m,
            new DateOnly(2026, 2, 23),
            [new DeterministicClassificationSubcategory(Guid.NewGuid(), "Income")]);

        var result = _engine.Execute(request);

        Assert.Equal(DeterministicClassificationReasonCodes.NonExpenseAmount, result.RationaleCode);
        Assert.Null(result.ProposedSubcategoryId);
        Assert.False(result.HasConflict);
    }
}

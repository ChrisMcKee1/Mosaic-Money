using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class FoundryClassificationPromptContextTests
{
    [Fact]
    public void BuildPrompt_IncludesPlaidContextBlock_WhenContextIsPresent()
    {
        var input = new FoundryClassificationInput(
            Guid.CreateVersion7(),
            "Coffee shop purchase",
            -8.75m,
            new DateOnly(2026, 2, 27),
            [new DeterministicClassificationSubcategory(Guid.CreateVersion7(), "Coffee")],
            "Platform + HouseholdShared scope",
            new FoundryPlaidContext(
                MerchantName: "Brew House",
                PaymentChannel: "in store",
                CategoryPrimary: "FOOD_AND_DRINK",
                CategoryDetailed: "FOOD_AND_DRINK_COFFEE",
                CounterpartyName: "Brew House",
                CounterpartyType: "merchant"));

        var prompt = FoundryClassificationService.BuildPrompt(input);

        Assert.Contains("Optional Plaid context (safe, non-secret hints; may be absent):", prompt, StringComparison.Ordinal);
        Assert.Contains("- merchantName: Brew House", prompt, StringComparison.Ordinal);
        Assert.Contains("- paymentChannel: in store", prompt, StringComparison.Ordinal);
        Assert.Contains("- categoryPrimary: FOOD_AND_DRINK", prompt, StringComparison.Ordinal);
        Assert.Contains("- categoryDetailed: FOOD_AND_DRINK_COFFEE", prompt, StringComparison.Ordinal);
        Assert.Contains("- counterpartyName: Brew House", prompt, StringComparison.Ordinal);
        Assert.Contains("- counterpartyType: merchant", prompt, StringComparison.Ordinal);
        Assert.Contains("Return strict JSON only with this shape:", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPrompt_OmitsPlaidContextBlock_WhenContextIsAbsent()
    {
        var input = new FoundryClassificationInput(
            Guid.CreateVersion7(),
            "Coffee shop purchase",
            -8.75m,
            new DateOnly(2026, 2, 27),
            [new DeterministicClassificationSubcategory(Guid.CreateVersion7(), "Coffee")],
            "Platform + HouseholdShared scope");

        var prompt = FoundryClassificationService.BuildPrompt(input);

        Assert.DoesNotContain("Optional Plaid context (safe, non-secret hints; may be absent):", prompt, StringComparison.Ordinal);
        Assert.Contains("Return strict JSON only with this shape:", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildPlaidContext_ReturnsNull_WhenPayloadIsMalformed()
    {
        var context = FoundryClassificationOrchestrator.TryBuildPlaidContext("{\"merchant_name\":\"Brew House\"");

        Assert.Null(context);
    }
}
